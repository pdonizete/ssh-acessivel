using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SshAcessivel.App.Input;
using SshAcessivel.App.Resources;
using SshAcessivel.Core.Output;
using SshAcessivel.Core.Persistence;
using SshAcessivel.Core.Speech;
using SshAcessivel.Core.Ssh;
using Microsoft.Win32;

namespace SshAcessivel.App;

/// <summary>Janela principal: campo de comando, campo de saída e barra de status.</summary>
internal sealed class MainForm : Form
{
    // O limite de linhas do campo de saída vem de _settings.MaxOutputLines.
    // As linhas antigas são descartadas em blocos de 500, para não reescrever o texto a cada pacote.
    private const int TrimSlackLines = 500;

    // Enviado no primeiro prompt para man, git log etc. não abrirem o less.
    // O espaço inicial evita que o comando vá para o histórico do bash (HISTCONTROL=ignorespace, padrão no Ubuntu e Debian).
    private const string PagerCommand = " export PAGER=cat GIT_PAGER=cat";

    private readonly IScreenReader _reader;
    private readonly ISshSession _session;
    private readonly OutputPipeline _pipeline = new();
    private readonly SpeechQueue _speech;
    private readonly TextBox _txtCommand;
    private readonly TextBox _txtOutput;
    private readonly ToolStripStatusLabel _lblStatus;
    private readonly ToolStripMenuItem _miDisconnect;
    private readonly ToolStripMenuItem _miManager;
    private readonly ToolStripMenuItem _miReconnect;
    private readonly ToolStripMenuItem _miStopLog;

    private int _outputLineCount;
    // Tamanho da linha parcial (sem \n) no fim do campo de saída. Ela é reescrita a cada pacote.
    private int _partialLength;
    private bool _passwordMode;
    private bool _fullScreenWarned;
    private readonly ConnectionStore _store = new(AppPaths.ConnectionsFile);
    private readonly KnownHostsStore _knownHosts = new(AppPaths.KnownHostsFile);
    private readonly AppSettings _settings = AppSettings.Load(AppPaths.SettingsFile);
    // Última conexão pedida, guardada só na memória (inclusive a senha digitada), para Ctrl+R
    // reconectar sem perguntar de novo. Nunca vai para disco.
    private SshConnectionRequest? _lastRequest;
    private bool _connectInProgress;
    // A chave recebida é diferente da salva: a mensagem de estado vira o alerta de bloqueio.
    private volatile bool _hostKeyMismatch;
    private KeyBindings _bindings;
    // Histórico do servidor atual. Antes da primeira conexão, um histórico vazio só na memória.
    private CommandHistory _history;
    private string? _historyPath;
    private string _historyServer = "";
    // Última resposta completa (linhas recebidas depois do último comando), para reler e copiar.
    // Usada só na thread da interface.
    private readonly StringBuilder _lastResponse = new();
    private SessionLog? _sessionLog;

    public MainForm(IScreenReader reader, ISshSession session, string initialStatus)
    {
        _reader = reader;
        _session = session;
        _speech = new SpeechQueue(reader, BuildSpeechOptions());
        _bindings = KeyBindings.FromSettings(_settings.Shortcuts);
        _history = new CommandHistory(_settings.HistorySize);

        Text = Strings.AppName;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(900, 600);
        MinimumSize = new Size(500, 350);

        // Menus nativos: o NVDA lê o nome e o atalho de cada item.
        // Os atalhos são configuráveis e tratados em ProcessCmdKey; o menu só mostra o texto da tecla.
        _miManager = new ToolStripMenuItem(Strings.Menu_Manager, null, OnManagerClick);
        _miReconnect = new ToolStripMenuItem(Strings.Menu_Reconnect, null, OnReconnectClick);
        _miDisconnect = new ToolStripMenuItem(Strings.Menu_Disconnect, null, OnDisconnectClick) { Enabled = false };
        var miExit = new ToolStripMenuItem(Strings.Menu_Exit, null, (_, _) => Close());
        var miConnection = new ToolStripMenuItem(Strings.Menu_Connection);
        miConnection.DropDownItems.AddRange([_miManager, _miReconnect, _miDisconnect, new ToolStripSeparator(), miExit]);
        var miSettings = new ToolStripMenuItem(Strings.Menu_Settings, null, OnSettingsClick);
        var miSessionLog = new ToolStripMenuItem(Strings.Menu_SessionLog, null, OnSessionLogClick);
        _miStopLog = new ToolStripMenuItem(Strings.Menu_StopSessionLog, null, OnStopSessionLogClick) { Enabled = false };
        var miOpenLogs = new ToolStripMenuItem(Strings.Menu_OpenLogs, null, OnOpenLogsClick);
        var miTools = new ToolStripMenuItem(Strings.Menu_Tools);
        miTools.DropDownItems.AddRange([miSettings, new ToolStripSeparator(), miSessionLog, _miStopLog, miOpenLogs]);
        var miShortcuts = new ToolStripMenuItem(Strings.Menu_Shortcuts, null, OnShortcutsClick);
        var miTestNvda = new ToolStripMenuItem(Strings.Menu_TestNvda, null, OnTestNvdaClick);
        var miHelp = new ToolStripMenuItem(Strings.Menu_Help);
        miHelp.DropDownItems.AddRange([miShortcuts, miTestNvda]);
        var menu = new MenuStrip();
        menu.Items.AddRange([miConnection, miTools, miHelp]);
        MainMenuStrip = menu;

        // Ordem de Tab: rótulo do comando, comando, rótulo da saída, saída.
        var lblCommand = new Label { Text = Strings.Main_CommandLabel, AutoSize = true, UseMnemonic = true, TabIndex = 0 };
        _txtCommand = new TextBox { AccessibleName = Strings.Main_CommandName, Dock = DockStyle.Fill, TabIndex = 1 };
        _txtCommand.KeyDown += OnCommandKeyDown;

        var lblOutput = new Label { Text = Strings.Main_OutputLabel, AutoSize = true, UseMnemonic = true, TabIndex = 2 };
        _txtOutput = new TextBox
        {
            AccessibleName = Strings.Main_OutputName,
            Multiline = true,
            ReadOnly = true,
            // Sem quebra automática: cada linha do campo é uma linha real do servidor.
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            // O padrão (32767) também limita o texto adicionado por código. O limite real é MaxOutputLines.
            MaxLength = int.MaxValue,
            Font = new Font(FontFamily.GenericMonospace, 10f),
            Dock = DockStyle.Fill,
            TabIndex = 3,
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(6), TabIndex = 0 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(lblCommand, 0, 0);
        layout.Controls.Add(_txtCommand, 0, 1);
        layout.Controls.Add(lblOutput, 0, 2);
        layout.Controls.Add(_txtOutput, 0, 3);

        _lblStatus = new ToolStripStatusLabel { Text = initialStatus, Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        var statusStrip = new StatusStrip { SizingGrip = false, AccessibleName = Strings.StatusBar_Name };
        statusStrip.Items.Add(_lblStatus);

        Controls.Add(layout);
        Controls.Add(statusStrip);
        Controls.Add(menu);
        ActiveControl = _txtCommand;

        _session.HostKeyValidator = ConfirmHostKey;
        _session.StateChanged += OnSessionStateChanged;
        _session.DataReceived += OnSessionData;
        // Evento estático do Windows: precisa ser removido ao fechar a janela.
        SystemEvents.SessionSwitch += OnSessionSwitch;
        _store.Load();
        UpdateMenuShortcuts();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_store.LoadFailed)
        {
            var message = _store.BackupPath is { } backup
                ? string.Format(Strings.Store_LoadProblem, backup)
                : string.Format(Strings.Store_LoadProblemNoBackup, AppPaths.ConnectionsFile);
            MessageBox.Show(this, message, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _session.StateChanged -= OnSessionStateChanged;
        _session.DataReceived -= OnSessionData;
        _session.HostKeyValidator = null;
        // Na saída do app, o aviso de desconexão ao servidor leva poucos milissegundos.
        _session.Dispose();
        _speech.Dispose();
        _sessionLog?.Dispose();
        base.OnFormClosed(e);
    }

    // Com o Windows bloqueado, nada é falado, para não expor dados na tela de bloqueio.
    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            _speech.Muted = true;
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            _speech.Muted = false;
        }
    }

    // ---- Conexão ----

    // "async void" só é usado em handlers de evento. "await" libera a thread da interface
    // enquanto espera, como await no asyncio.
    private void OnManagerClick(object? sender, EventArgs e)
    {
        if (_connectInProgress || _session.State == ConnectionState.Connecting)
        {
            return;
        }
        ConnectionProfile? chosen = null;
        using (var manager = new ConnectionManagerForm(_store))
        {
            if (manager.ShowDialog(this) == DialogResult.OK)
            {
                chosen = manager.SelectedProfile;
            }
        }
        if (chosen is null)
        {
            _txtCommand.Focus();
            return;
        }
        ConnectProfile(chosen);
    }

    private async void ConnectProfile(ConnectionProfile profile)
    {
        _connectInProgress = true;
        try
        {
            var request = await BuildRequestAsync(profile);
            if (request is not null)
            {
                await StartConnectionAsync(request);
            }
        }
        finally
        {
            _connectInProgress = false;
            _txtCommand.Focus();
        }
    }

    private async void OnReconnectClick(object? sender, EventArgs e)
    {
        if (_connectInProgress || _session.State == ConnectionState.Connecting)
        {
            return;
        }
        if (_lastRequest is not { } request)
        {
            Announce(Strings.Reconnect_NoPrevious);
            return;
        }
        _connectInProgress = true;
        try
        {
            await StartConnectionAsync(request);
        }
        finally
        {
            _connectInProgress = false;
            _txtCommand.Focus();
        }
    }

    private async Task StartConnectionAsync(SshConnectionRequest request)
    {
        _lastRequest = request;
        _historyServer = request.ToString();
        var historyPath = CommandHistory.FileFor(AppPaths.HistoryDirectory, request.Host, request.Port, request.Username);
        _historyPath = historyPath;
        int historySize = _settings.HistorySize;
        // Lê o histórico deste servidor fora da thread da interface.
        _history = await Task.Run(() => CommandHistory.Load(historyPath, historySize));
        _lastResponse.Clear();
        if (_session.State == ConnectionState.Connected)
        {
            await _session.DisconnectAsync();
        }
        _hostKeyMismatch = false;
        _pipeline.Reset();
        _pipeline.ScheduleHiddenCommand(PagerCommand);
        // Task.Run: DNS, socket e troca de chaves rodam numa thread de fundo.
        await Task.Run(() => _session.ConnectAsync(request));
    }

    /// <summary>Monta o pedido de conexão: decifra o segredo salvo ou pergunta ao usuário. Null = cancelado.</summary>
    private async Task<SshConnectionRequest?> BuildRequestAsync(ConnectionProfile profile)
    {
        string? secret = null;
        if (profile.HasSavedSecret && SecretProtector.TryUnprotect(profile.ProtectedSecret, out var saved))
        {
            secret = saved;
        }
        var baseRequest = new SshConnectionRequest(profile.Host, profile.Port, profile.Username)
        {
            ConnectTimeout = TimeSpan.FromSeconds(_settings.ConnectTimeoutSeconds),
            KeepAliveInterval = _settings.KeepAliveSeconds == 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(_settings.KeepAliveSeconds),
        };

        // "with" cria uma cópia do record com alguns campos trocados, como dataclasses.replace() no Python.
        if (profile.Authentication == AuthenticationKind.Password)
        {
            secret ??= SecretPromptForm.Ask(this, Strings.Prompt_PasswordTitle,
                                            string.Format(Strings.Prompt_PasswordLabel, profile.Username, profile.Host));
            return secret is null ? null : baseRequest with { Password = secret };
        }

        var keyPath = profile.PrivateKeyPath ?? "";
        var keyName = Path.GetFileName(keyPath);
        string? prompt = null;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (prompt is not null)
            {
                secret = SecretPromptForm.Ask(this, Strings.Prompt_PassphraseTitle, prompt);
                if (secret is null)
                {
                    return null;
                }
            }
            var passphrase = secret;
            // Lê o arquivo da chave fora da thread da interface.
            var check = await Task.Run(() => PrivateKeyLoader.Check(keyPath, passphrase));
            switch (check)
            {
                case PrivateKeyCheck.Ok:
                    return baseRequest with { PrivateKeyPath = keyPath, Passphrase = passphrase };
                case PrivateKeyCheck.PassphraseRequired:
                    prompt = string.Format(Strings.Prompt_PassphraseLabel, keyName);
                    break;
                case PrivateKeyCheck.InvalidOrWrongPassphrase when !string.IsNullOrEmpty(passphrase):
                    prompt = string.Format(Strings.Prompt_WrongPassphrase, keyName);
                    break;
                case PrivateKeyCheck.NotFound:
                    Announce(string.Format(Strings.Key_NotFound, keyPath));
                    return null;
                case PrivateKeyCheck.Unreadable:
                    Announce(string.Format(Strings.Key_Unreadable, keyPath));
                    return null;
                default:
                    Announce(string.Format(Strings.Key_Invalid, keyPath));
                    return null;
            }
        }
        Announce(string.Format(Strings.Key_Invalid, keyPath));
        return null;
    }

    // Roda na thread da interface (via Invoke em ConfirmHostKey).
    private bool ShowHostKeyMismatch(HostKeyInfo info)
    {
        _hostKeyMismatch = true;
        _speech.SpeakNow(Strings.HostKey_MismatchSpeech);
        var message = string.Format(Strings.HostKey_MismatchMessage, info.Host, info.Port, info.KeyType,
                                    info.DisplayFingerprint, _knownHosts.FilePath);
        MessageBox.Show(this, message, Strings.HostKey_MismatchTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
    }

    private bool AskHostKey(HostKeyInfo info)
    {
        HostKeyDecision decision;
        using (var form = new HostKeyForm(info))
        {
            decision = form.ShowDialog(this) == DialogResult.OK ? form.Decision : HostKeyDecision.Cancel;
        }
        if (decision == HostKeyDecision.AcceptAndSave)
        {
            try
            {
                _knownHosts.Add(info.Host, info.Port, info.KeyType, info.DisplayFingerprint);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, string.Format(Strings.HostKey_SaveFailed, ex.Message), Strings.AppName,
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        return decision != HostKeyDecision.Cancel;
    }

    private async void OnDisconnectClick(object? sender, EventArgs e) => await _session.DisconnectAsync();

    // Chamado na thread da conexão. Invoke (síncrono) mostra o diálogo na thread da interface
    // e faz a thread da conexão esperar a resposta do usuário.
    private bool ConfirmHostKey(HostKeyInfo info)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return false;
        }
        HostKeyCheck check;
        try
        {
            check = _knownHosts.Check(info.Host, info.Port, info.DisplayFingerprint);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            check = HostKeyCheck.Unknown; // arquivo ilegível: pergunta ao usuário, como numa primeira conexão
        }
        if (check == HostKeyCheck.Trusted)
        {
            return true;
        }
        try
        {
            return Invoke(() => check == HostKeyCheck.Mismatch ? ShowHostKeyMismatch(info) : AskHostKey(info));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return false; // janela fechando
        }
    }

    // ---- Eventos da sessão (chegam em threads de fundo) ----

    private void OnSessionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        var message = DescribeState(e);
        // Fala aqui mesmo, antes de a leitura começar: assim "Conectado" vem antes da mensagem do servidor.
        _speech.SpeakNow(message);
        PostToUi(() => ApplyState(e, message));
    }

    private void OnSessionData(object? sender, byte[] data)
    {
        // Decodificação e fala rodam na thread de leitura; a interface só recebe o texto pronto.
        var result = _pipeline.Process(data);
        if (result.PasswordPrompt)
        {
            _speech.SpeakNow(Strings.Speech_PasswordRequested);
        }
        else if (result.Speech.Length > 0)
        {
            _speech.Enqueue(result.Speech);
        }
        if (result.ProgressPercent is int percent)
        {
            _speech.ReportProgress(percent);
        }
        if (result.CommandToSend is { } command)
        {
            _session.SendLine(command);
        }
        if (result.HasDisplayChange || result.PasswordPrompt || result.AlternateScreenEntered)
        {
            PostToUi(() => ApplyOutput(result));
        }
    }

    // BeginInvoke coloca a função na fila da thread da interface e retorna na hora,
    // como loop.call_soon_threadsafe no asyncio.
    private void PostToUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }
        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // A janela está fechando.
        }
    }

    private string DescribeState(ConnectionStateChangedEventArgs e) => e.State switch
    {
        ConnectionState.HostKeyRejected when _hostKeyMismatch => Strings.State_HostKeyMismatch,
        ConnectionState.Connecting => string.Format(Strings.State_Connecting, e.Detail),
        ConnectionState.Connected => string.Format(Strings.State_Connected, e.Detail),
        ConnectionState.Disconnected => Strings.State_Disconnected,
        ConnectionState.AuthenticationFailed => Strings.State_AuthFailed,
        ConnectionState.HostKeyRejected => Strings.State_HostKeyRejected,
        ConnectionState.TimedOut => Strings.State_TimedOut,
        ConnectionState.ConnectionLost => Strings.State_ConnectionLost,
        _ => string.Format(Strings.State_Error, e.Detail),
    };

    private void ApplyState(ConnectionStateChangedEventArgs e, string message)
    {
        _lblStatus.Text = message;
        _miDisconnect.Enabled = e.State == ConnectionState.Connected;
        Text = e.State == ConnectionState.Connected ? string.Format(Strings.Main_TitleConnected, e.Detail) : Strings.AppName;
        if (e.State != ConnectionState.Connected)
        {
            FreezePartial();
            ExitPasswordMode();
        }
        if (e.State is ConnectionState.AuthenticationFailed or ConnectionState.HostKeyRejected or ConnectionState.TimedOut
            or ConnectionState.Error or ConnectionState.ConnectionLost)
        {
            // Só o destino (usuario@host:porta) e a mensagem do erro; nunca a senha.
            AppLog.Error($"Conexão {_lastRequest}: {e.State}. {e.Detail}");
        }
    }

    private void ApplyOutput(OutputResult result)
    {
        if (result.HasDisplayChange)
        {
            ApplyDisplay(result.Committed, result.Partial);
        }
        if (result.Response.Length > 0)
        {
            _lastResponse.Append(result.Response);
        }
        if (result.Committed.Length > 0)
        {
            WriteSessionLog(result.Committed);
        }
        if (result.PasswordPrompt)
        {
            EnterPasswordMode();
        }
        if (result.AlternateScreenEntered && !_fullScreenWarned)
        {
            _fullScreenWarned = true;
            Announce(Strings.Speech_FullScreenDetected);
        }
    }

    /// <summary>Mostra na barra de status e fala, interrompendo a fala anterior.</summary>
    private void Announce(string message)
    {
        _lblStatus.Text = message;
        _speech.SpeakNow(message);
    }

    // ---- Campo de comando ----

    private void OnCommandKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None)
        {
            // SuppressKeyPress evita o bipe do Windows ao teclar Enter num campo de uma linha.
            e.SuppressKeyPress = true;
            SendCommand();
        }
    }

    private void SendCommand()
    {
        var text = _txtCommand.Text;
        if (_session.State != ConnectionState.Connected)
        {
            Announce(Strings.State_NotConnected);
            return;
        }
        if (_passwordMode)
        {
            // Senha: sem eco esperado, fora do histórico, da última resposta e do log.
            _session.SendLine(text);
            _txtCommand.Clear();
            ExitPasswordMode();
            return;
        }

        _pipeline.NotifyCommandSent(text);
        // SendLine só põe o texto numa fila; o envio pela rede acontece numa tarefa de fundo.
        if (!_session.SendLine(text))
        {
            Announce(Strings.State_NotConnected);
            return;
        }
        _txtCommand.Clear();
        _history.Add(text);
        if (_historyPath is { } historyPath)
        {
            // Grava numa tarefa de fundo; erros vão para o log do app.
            _ = _history.SaveAsync(historyPath, _historyServer);
        }
        _lastResponse.Clear();
        _fullScreenWarned = false;
        if (FullScreenDetector.DetectProgram(text) is { } program)
        {
            _fullScreenWarned = true;
            Announce(string.Format(Strings.Speech_FullScreenCommand, program));
        }
    }

    // UseSystemPasswordChar marca o campo como protegido: o NVDA não lê os caracteres digitados.
    private void EnterPasswordMode()
    {
        _passwordMode = true;
        _txtCommand.UseSystemPasswordChar = true;
        _txtCommand.AccessibleName = Strings.Main_PasswordName;
    }

    private void ExitPasswordMode()
    {
        if (!_passwordMode)
        {
            return;
        }
        _passwordMode = false;
        _txtCommand.UseSystemPasswordChar = false;
        _txtCommand.AccessibleName = Strings.Main_CommandName;
    }

    // ---- Campo de saída ----

    /// <summary>
    /// Troca a linha parcial do fim pelo texto novo, sem tirar o foco de onde está.
    /// Se o usuário estiver lendo a saída, o cursor e a seleção voltam ao ponto em que estavam.
    /// </summary>
    private void ApplyDisplay(string committed, string partial)
    {
        var box = _txtOutput;
        bool userReading = box.Focused;
        int selStart = box.SelectionStart;
        int selLength = box.SelectionLength;

        // Select + SelectedText troca só o fim do texto, sem reescrever o resto. Funciona com ReadOnly.
        int tailLength = Math.Min(_partialLength, box.TextLength);
        box.Select(box.TextLength - tailLength, tailLength);
        box.SelectedText = committed + partial;
        _partialLength = partial.Length;
        _outputLineCount += CountLineBreaks(committed);
        int removedChars = TrimOldLines();

        if (userReading)
        {
            int length = box.TextLength;
            int start = Math.Clamp(selStart - removedChars, 0, length);
            int end = Math.Clamp(selStart + selLength - removedChars, start, length);
            box.Select(start, end - start);
        }
        else
        {
            box.Select(box.TextLength, 0);
            box.ScrollToCaret();
        }
    }

    /// <summary>Fixa a linha parcial (ex.: último prompt) quando a conexão termina.</summary>
    private void FreezePartial()
    {
        if (_partialLength == 0)
        {
            return;
        }
        var text = _txtOutput.Text;
        ApplyDisplay(text[^Math.Min(_partialLength, text.Length)..] + "\r\n", "");
    }

    /// <summary>Descarta as linhas mais antigas acima do limite. Retorna quantos caracteres saíram do início.</summary>
    private int TrimOldLines()
    {
        int maxLines = _settings.MaxOutputLines;
        if (_outputLineCount <= maxLines + TrimSlackLines)
        {
            return 0;
        }
        int linesToRemove = _outputLineCount - maxLines;
        var text = _txtOutput.Text;
        int index = -1;
        for (int i = 0; i < linesToRemove; i++)
        {
            index = text.IndexOf('\n', index + 1);
            if (index < 0)
            {
                return 0;
            }
        }
        int cut = index + 1;
        _txtOutput.Select(0, cut);
        _txtOutput.SelectedText = string.Empty;
        _outputLineCount -= linesToRemove;
        return cut;
    }

    private static int CountLineBreaks(string text)
    {
        int count = 0;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                count++;
            }
        }
        return count;
    }

    // ---- Atalhos de teclado ----

    // ProcessCmdKey recebe a tecla antes do controle com foco, inclusive Tab, setas e Alt+número.
    // Retornar true consome a tecla; base.ProcessCmdKey mantém o comportamento normal do Windows.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_bindings.FindAction(keyData) is { } action && TryRunAction(action))
        {
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>Executa a ação. Retorna false quando ela não se aplica ao foco atual, e a tecla segue o caminho normal.</summary>
    private bool TryRunAction(ShortcutAction action)
    {
        bool inCommand = _txtCommand.Focused;
        switch (action)
        {
            case ShortcutAction.ToggleFocus:
                if (inCommand)
                {
                    _txtOutput.Focus();
                }
                else
                {
                    _txtCommand.Focus();
                }
                return true;
            case ShortcutAction.HistoryPrevious when inCommand && !_passwordMode:
                ShowHistory(_history.Previous(_txtCommand.Text));
                return true;
            case ShortcutAction.HistoryNext when inCommand && !_passwordMode:
                ShowHistory(_history.Next());
                return true;
            case ShortcutAction.SendInterrupt when inCommand && _txtCommand.SelectionLength == 0:
                SendInterrupt();
                return true;
            case ShortcutAction.SendTab when inCommand && !_passwordMode && _settings.TabSendsToServer:
                SendTab();
                return true;
            case ShortcutAction.CancelSpeech:
                _speech.Cancel();
                return true;
            case ShortcutAction.ToggleAutoRead:
                _speech.AutoRead = !_speech.AutoRead;
                Say(_speech.AutoRead ? Strings.Speech_AutoReadOn : Strings.Speech_AutoReadOff);
                return true;
            case ShortcutAction.RereadResponse:
                RereadResponse();
                return true;
            case ShortcutAction.CopyResponse:
                CopyResponse();
                return true;
            case ShortcutAction.ClearOutput:
                _txtOutput.Clear();
                _outputLineCount = 0;
                _partialLength = 0;
                Say(Strings.Speech_OutputCleared);
                return true;
            case >= ShortcutAction.ReadLine1 and <= ShortcutAction.ReadLine9:
                ReadRecentLine(action - ShortcutAction.ReadLine1 + 1);
                return true;
            case ShortcutAction.ConnectionManager:
                OnManagerClick(this, EventArgs.Empty);
                return true;
            case ShortcutAction.Reconnect:
                OnReconnectClick(this, EventArgs.Empty);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Fala sem mudar a barra de status (que mostra o estado da conexão).</summary>
    private void Say(string message) => _speech.SpeakNow(message);

    private void ShowHistory(string? text)
    {
        if (text is null)
        {
            Say(_history.Entries.Count == 0 ? Strings.Speech_HistoryEmpty : Strings.Speech_HistoryDraft);
            return;
        }
        _txtCommand.Text = text;
        _txtCommand.SelectionStart = text.Length;
        // O NVDA nem sempre anuncia a troca do texto de um campo; falamos o item.
        Say(text.Length == 0 ? Strings.Speech_HistoryDraft : text);
    }

    private void SendInterrupt()
    {
        if (!_session.SendRaw("\u0003"))
        {
            Announce(Strings.State_NotConnected);
            return;
        }
        _txtCommand.Clear();
        ExitPasswordMode();
        _speech.Cancel();
    }

    // O texto digitado vai junto com o Tab: o shell remoto completa e mostra a linha na saída.
    // O que for digitado depois continua a mesma linha no servidor.
    private void SendTab()
    {
        if (!_session.SendRaw(_txtCommand.Text + "\t"))
        {
            Announce(Strings.State_NotConnected);
            return;
        }
        _txtCommand.Clear();
    }

    private void RereadResponse()
    {
        var text = _lastResponse.ToString();
        if (text.Length == 0)
        {
            Say(Strings.Speech_NoResponse);
        }
        else if (string.IsNullOrWhiteSpace(text))
        {
            Say(Strings.Speech_EmptyResponse);
        }
        else
        {
            Say(text);
        }
    }

    private void CopyResponse()
    {
        var text = _lastResponse.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            Say(text.Length == 0 ? Strings.Speech_NoResponse : Strings.Speech_EmptyResponse);
            return;
        }
        try
        {
            Clipboard.SetText(text);
            Say(string.Format(Strings.Speech_ResponseCopied, CountLineBreaks(text)));
        }
        catch (ExternalException ex)
        {
            AppLog.Error("Falha ao copiar para a área de transferência", ex);
            Say(Strings.Speech_CopyFailed);
        }
    }

    private void ReadRecentLine(int n)
    {
        // Só as linhas completas: a linha parcial do fim (o prompt) não conta.
        var text = _txtOutput.Text;
        int end = text.Length - Math.Min(_partialLength, text.Length);
        var lines = text[..end].Split("\r\n");
        int count = lines.Length > 0 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
        if (n > count)
        {
            Say(string.Format(Strings.Speech_NoLine, n));
            return;
        }
        var line = lines[count - n];
        Say(string.IsNullOrWhiteSpace(line) ? Strings.Speech_BlankLine : line);
    }

    private void UpdateMenuShortcuts()
    {
        _miManager.ShortcutKeyDisplayString = MenuKeyText(ShortcutAction.ConnectionManager);
        _miReconnect.ShortcutKeyDisplayString = MenuKeyText(ShortcutAction.Reconnect);
    }

    private string? MenuKeyText(ShortcutAction action) =>
        _bindings[action] == Keys.None ? null : KeyBindings.Display(_bindings[action]);

    private SpeechQueueOptions BuildSpeechOptions()
    {
        int debounce = _settings.SpeechDebounceMilliseconds;
        return new SpeechQueueOptions
        {
            Debounce = TimeSpan.FromMilliseconds(debounce),
            // A espera máxima precisa ser maior que a pausa, senão a pausa não teria efeito.
            MaxDelay = TimeSpan.FromMilliseconds(Math.Max(1000, debounce * 4)),
            SummaryLineThreshold = _settings.SummaryLineThreshold,
            ProgressInterval = TimeSpan.FromSeconds(_settings.ProgressIntervalSeconds),
            SummaryFormatter = lines => string.Format(Strings.Speech_LinesReceived, lines),
            ProgressFormatter = percent => string.Format(Strings.Speech_Progress, percent),
        };
    }

    // ---- Ferramentas e Ajuda ----

    private void OnSettingsClick(object? sender, EventArgs e)
    {
        using (var form = new SettingsForm(_settings))
        {
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                _speech.Options = BuildSpeechOptions();
                _history.SetMaxEntries(_settings.HistorySize);
                SaveSettings();
            }
        }
        _txtCommand.Focus();
    }

    private void OnShortcutsClick(object? sender, EventArgs e)
    {
        using (var form = new ShortcutsForm(_bindings))
        {
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                _bindings = form.Bindings;
                _settings.Shortcuts = _bindings.ToSettings();
                SaveSettings();
                UpdateMenuShortcuts();
            }
        }
        _txtCommand.Focus();
    }

    private void SaveSettings()
    {
        try
        {
            _settings.Save(AppPaths.SettingsFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Falha ao salvar as configurações", ex);
            MessageBox.Show(this, string.Format(Strings.Settings_SaveFailed, ex.Message), Strings.AppName,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSessionLogClick(object? sender, EventArgs e)
    {
        using (var dialog = new SaveFileDialog
        {
            Title = Strings.SessionLog_SaveTitle,
            Filter = Strings.SessionLog_Filter,
            FileName = string.Format(Strings.SessionLog_FileName, DateTime.Now),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        })
        {
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var log = new SessionLog(dialog.FileName);
                    _sessionLog?.Dispose();
                    _sessionLog = log;
                    _miStopLog.Enabled = true;
                    Say(string.Format(Strings.SessionLog_Started, log.FilePath));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    AppLog.Error("Falha ao abrir o log da sessão", ex);
                    MessageBox.Show(this, string.Format(Strings.SessionLog_Failed, ex.Message), Strings.AppName,
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        _txtCommand.Focus();
    }

    private void OnStopSessionLogClick(object? sender, EventArgs e)
    {
        _sessionLog?.Dispose();
        _sessionLog = null;
        _miStopLog.Enabled = false;
        Say(Strings.SessionLog_Stopped);
    }

    private void WriteSessionLog(string text)
    {
        if (_sessionLog is not { } log)
        {
            return;
        }
        log.Write(text);
        if (log.Failed)
        {
            _sessionLog = null;
            log.Dispose();
            _miStopLog.Enabled = false;
            Say(string.Format(Strings.SessionLog_Failed, log.FilePath));
        }
    }

    private void OnOpenLogsClick(object? sender, EventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppLog.LogsDirectory);
            // UseShellExecute abre a pasta no Explorador, como os.startfile() no Python.
            Process.Start(new ProcessStartInfo { FileName = AppLog.LogsDirectory, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Say(ex.Message);
        }
    }

    private void OnTestNvdaClick(object? sender, EventArgs e)
    {
        using var form = new NvdaTestForm(_reader, _lblStatus.Text ?? string.Empty);
        form.ShowDialog(this);
    }
}
