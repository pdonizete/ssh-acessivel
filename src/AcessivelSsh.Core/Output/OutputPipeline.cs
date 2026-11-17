using System.Text;
using System.Text.RegularExpressions;

namespace AcessivelSsh.Core.Output;

/// <summary>Resultado do processamento de um pacote recebido do servidor.</summary>
/// <param name="Committed">Linhas que terminaram neste pacote, cada uma com \r\n.</param>
/// <param name="Partial">Linha atual, sem \n. Na tela, substitui a linha parcial anterior.</param>
/// <param name="Speech">Só o texto novo a falar, sem o eco do comando.</param>
/// <param name="ProgressPercent">Porcentagem de uma barra de progresso que foi reescrita.</param>
/// <param name="CommandToSend">Comando oculto que deve ser enviado agora (PAGER).</param>
/// <param name="Response">Linhas completas que terminaram neste pacote, sem a linha do eco (para reler e copiar).</param>
public sealed record OutputResult(
    string Committed,
    string Partial,
    bool PartialChanged,
    string Speech,
    int? ProgressPercent,
    bool PasswordPrompt,
    bool AlternateScreenEntered,
    string? CommandToSend,
    string Response)
{
    public bool HasDisplayChange => Committed.Length > 0 || PartialChanged;

    public static OutputResult Unchanged(string partial) => new("", partial, false, "", null, false, false, null, "");
}

/// <summary>
/// Converte os bytes do servidor em texto para exibir e falar:
/// UTF-8 com estado, remoção de ANSI, tratamento de \r, eco do comando, prompts de senha.
/// </summary>
/// <remarks>
/// Process roda na thread de leitura; NotifyCommandSent e Reset vêm da thread da interface.
/// Um lock protege o estado, como um threading.Lock no Python.
/// </remarks>
public sealed partial class OutputPipeline
{
    private readonly object _lock = new();
    // O Decoder guarda os bytes de um caractere incompleto (ex.: "ç" = C3 A7 dividido entre
    // dois pacotes) até o próximo pedaço chegar. Encoding.GetString por pedaço trocaria por "?".
    private readonly Decoder _decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetDecoder();
    private readonly LineAssembler _lines = new();
    private readonly List<string> _hiddenCommands = [];
    private string _ansiPending = "";
    private string? _expectedEcho;
    private string? _scheduledHiddenCommand;
    // Quantos caracteres da linha parcial atual já foram falados.
    private int _spokenPrefix;
    private bool _passwordPromptActive;

    [GeneratedRegex(@"(\d{1,3})(?:[.,]\d+)?\s?%")]
    private static partial Regex PercentRegex();

    // Prompt típico de shell: termina em $, #, > ou %.
    [GeneratedRegex(@"[$#>%]\s*$")]
    private static partial Regex ShellPromptRegex();

    public OutputResult Process(ReadOnlySpan<byte> bytes)
    {
        lock (_lock)
        {
            if (bytes.IsEmpty)
            {
                return OutputResult.Unchanged(_lines.Partial);
            }
            var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
            int count = _decoder.GetChars(bytes, chars, flush: false);
            return ProcessText(new string(chars, 0, count));
        }
    }

    /// <summary>Avisa que o usuário enviou um comando: a primeira linha que terminar com ele é eco e não é falada.</summary>
    public void NotifyCommandSent(string command)
    {
        lock (_lock)
        {
            var trimmed = command.Trim();
            _expectedEcho = trimmed.Length == 0 ? null : trimmed;
        }
    }

    /// <summary>
    /// Agenda um comando para o primeiro prompt do shell. O eco dele some da tela e da fala.
    /// </summary>
    public void ScheduleHiddenCommand(string command)
    {
        lock (_lock)
        {
            _scheduledHiddenCommand = command;
        }
    }

    /// <summary>Descarta todo o estado (usar antes de cada nova conexão).</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _decoder.Reset();
            _lines.Reset();
            _hiddenCommands.Clear();
            _ansiPending = "";
            _expectedEcho = null;
            _scheduledHiddenCommand = null;
            _spokenPrefix = 0;
            _passwordPromptActive = false;
        }
    }

    private OutputResult ProcessText(string text)
    {
        var ansi = AnsiStripper.Strip(_ansiPending.Length == 0 ? text : _ansiPending + text);
        _ansiPending = ansi.Pending;

        string oldPartial = _lines.Partial;
        var assembled = _lines.Process(ansi.Text);
        var committed = new StringBuilder();
        var speech = new StringBuilder();
        var response = new StringBuilder();
        foreach (var line in assembled.CompletedLines)
        {
            CommitLine(line, committed, speech, response);
        }

        string partial = _lines.Partial;
        bool partialChanged = assembled.CompletedLines.Count > 0 || partial != oldPartial;
        int? progress = null;
        bool passwordPrompt = false;
        string? commandToSend = null;
        _spokenPrefix = Math.Min(_spokenPrefix, partial.Length);

        if (assembled.PartialOverwritten)
        {
            // Barra de progresso: não fala cada atualização, só informa a porcentagem.
            _spokenPrefix = partial.Length;
            progress = ParseProgress(partial);
        }
        else if (!_passwordPromptActive && PasswordPromptDetector.IsPasswordPrompt(partial))
        {
            // Quem chama fala "Senha solicitada" em vez do texto do prompt.
            _passwordPromptActive = true;
            passwordPrompt = true;
            _spokenPrefix = partial.Length;
        }
        else if (_scheduledHiddenCommand is { } hidden && ShellPromptRegex().IsMatch(partial))
        {
            // Primeiro prompt: envia o comando oculto. Este prompt não é falado, porque
            // um novo aparece logo depois do comando.
            _scheduledHiddenCommand = null;
            _hiddenCommands.Add(hidden.Trim());
            _expectedEcho = hidden.Trim();
            commandToSend = hidden;
        }
        else if (_expectedEcho is null && !_passwordPromptActive && partial.Length > _spokenPrefix)
        {
            // Enquanto o eco do comando não chega, a linha parcial é o próprio comando: não falamos.
            speech.Append(partial, _spokenPrefix, partial.Length - _spokenPrefix);
            _spokenPrefix = partial.Length;
        }

        return new OutputResult(committed.ToString(), partial, partialChanged, speech.ToString(),
                                progress, passwordPrompt, ansi.AlternateScreenEntered, commandToSend, response.ToString());
    }

    private void CommitLine(CompletedLine line, StringBuilder committed, StringBuilder speech, StringBuilder response)
    {
        // Numa linha reescrita, o que já foi falado não vale mais: fala a versão final inteira.
        int spoken = line.WasOverwritten ? 0 : _spokenPrefix;
        var echo = _expectedEcho;
        _spokenPrefix = 0;
        _expectedEcho = null; // só a primeira linha depois do Enter pode ser o eco
        _passwordPromptActive = false;

        var trimmed = line.Text.TrimEnd();
        foreach (var hidden in _hiddenCommands)
        {
            if (trimmed.EndsWith(hidden, StringComparison.Ordinal))
            {
                return; // eco do comando oculto: some da tela e da fala
            }
        }
        committed.Append(line.Text).Append("\r\n");

        // A resposta guarda a linha inteira (mesmo a parte já falada), menos a linha do eco do comando.
        if (echo is null || !trimmed.EndsWith(echo, StringComparison.Ordinal))
        {
            response.Append(line.Text).Append("\r\n");
        }

        var remainder = spoken >= trimmed.Length ? "" : trimmed[spoken..];
        if (echo is not null && remainder.EndsWith(echo, StringComparison.Ordinal))
        {
            remainder = remainder[..^echo.Length];
        }
        if (!string.IsNullOrWhiteSpace(remainder))
        {
            speech.Append(remainder).Append("\r\n");
        }
    }

    private static int? ParseProgress(string line)
    {
        var matches = PercentRegex().Matches(line);
        if (matches.Count == 0)
        {
            return null;
        }
        return int.TryParse(matches[^1].Groups[1].ValueSpan, out var value) && value <= 100 ? value : null;
    }
}
