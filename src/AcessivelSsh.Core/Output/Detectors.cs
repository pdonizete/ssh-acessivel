using System.Text.RegularExpressions;

namespace AcessivelSsh.Core.Output;

/// <summary>Detecta pedidos de senha na última linha (a que ainda não terminou com \n).</summary>
public static partial class PasswordPromptDetector
{
    // A palavra-chave precisa estar perto de um ":" no FIM da linha. Assim "a senha foi alterada"
    // no meio de um texto não ativa o modo senha.
    // [GeneratedRegex] compila a expressão durante o build (como re.compile, mas feito uma vez só pelo compilador).
    [GeneratedRegex(@"(?:password|senha|passphrase)[^:\r\n]{0,60}:\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PromptRegex();

    public static bool IsPasswordPrompt(string line) =>
        line.Length is > 0 and <= 200 && PromptRegex().IsMatch(line);
}

/// <summary>Detecta comandos que abrem programas de tela cheia, não suportados na v1.</summary>
public static class FullScreenDetector
{
    private static readonly HashSet<string> Programs = new(StringComparer.Ordinal)
    {
        "vi", "vim", "nvim", "view", "vimdiff", "nano", "pico", "emacs", "htop", "top", "btop", "atop",
        "less", "more", "most", "mc", "tmux", "screen", "watch", "iftop", "nmtui", "alsamixer", "ncdu", "ranger",
    };

    // Palavras que vêm antes do programa de verdade ("sudo vim", "env X=1 nano").
    private static readonly HashSet<string> Prefixes = new(StringComparer.Ordinal)
    {
        "sudo", "doas", "env", "nohup", "time", "nice", "exec", "command",
    };

    /// <summary>Nome do programa de tela cheia encontrado no comando, ou null.</summary>
    public static string? DetectProgram(string commandLine)
    {
        // Cada trecho separado por |, ; ou & é um comando ("git log | less").
        foreach (var segment in commandLine.Split(['|', ';', '&'], StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var token in segment.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (Prefixes.Contains(token) || token.StartsWith('-') || token.Contains('='))
                {
                    continue;
                }
                var name = token[(token.LastIndexOf('/') + 1)..];
                if (Programs.Contains(name))
                {
                    return name;
                }
                break; // só a primeira palavra de cada trecho é o programa
            }
        }
        return null;
    }
}
