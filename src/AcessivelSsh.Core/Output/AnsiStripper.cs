using System.Text;

namespace AcessivelSsh.Core.Output;

/// <summary>Resultado de <see cref="AnsiStripper.Strip"/>.</summary>
/// <param name="Text">Texto sem as sequências de controle.</param>
/// <param name="Pending">Sequência incompleta no fim da entrada. Deve ser colocada antes do próximo pedaço.</param>
/// <param name="AlternateScreenEntered">O programa pediu a tela alternativa (vim, htop, less...).</param>
/// <param name="AlternateScreenExited">O programa saiu da tela alternativa.</param>
public readonly record struct AnsiStripResult(string Text, string Pending, bool AlternateScreenEntered, bool AlternateScreenExited);

/// <summary>
/// Remove sequências ANSI/VT100 (cores, cursor, título da janela, modos). Função pura: não guarda estado.
/// </summary>
public static class AnsiStripper
{
    private const char Esc = '\u001b';
    private const char Csi8Bit = '\u009b';
    // Uma sequência "incompleta" maior que isto é lixo: descartamos para não acumular memória.
    private const int MaxPendingLength = 4096;

    public static AnsiStripResult Strip(string input)
    {
        if (input.IndexOf(Esc) < 0 && input.IndexOf(Csi8Bit) < 0)
        {
            return new AnsiStripResult(input, "", false, false);
        }

        var sb = new StringBuilder(input.Length);
        bool altOn = false, altOff = false;
        int i = 0;
        while (i < input.Length)
        {
            char c = input[i];
            if (c != Esc && c != Csi8Bit)
            {
                sb.Append(c);
                i++;
                continue;
            }

            // "ref" passa a variável por referência: a função pode alterá-la, como um objeto mutável no Python.
            int end = c == Csi8Bit ? ParseCsi(input, i + 1, ref altOn, ref altOff) : ParseEscape(input, i, ref altOn, ref altOff);
            if (end < 0)
            {
                string pending = input[i..];
                return new AnsiStripResult(sb.ToString(), pending.Length > MaxPendingLength ? "" : pending, altOn, altOff);
            }
            i = end;
        }
        return new AnsiStripResult(sb.ToString(), "", altOn, altOff);
    }

    // Retorna o índice logo após a sequência, ou -1 se ela ainda não terminou (continua no próximo pacote).
    private static int ParseEscape(string s, int start, ref bool altOn, ref bool altOff)
    {
        if (start + 1 >= s.Length)
        {
            return -1;
        }
        switch (s[start + 1])
        {
            case '[':
                return ParseCsi(s, start + 2, ref altOn, ref altOff);
            case ']': // OSC, ex.: título da janela
            case 'P': // DCS
            case '^':
            case '_':
            case 'X':
                return ParseString(s, start + 2);
            case '(' or ')' or '*' or '+' or '-' or '.' or '/' or '#' or '%' or ' ':
                // Seleção de conjunto de caracteres e similares: ESC + 2 caracteres.
                return start + 2 < s.Length ? start + 3 : -1;
            default:
                // ESC + 1 caractere (ESC 7, ESC 8, ESC =, ESC >, ESC M...).
                return start + 2;
        }
    }

    // CSI: parâmetros (0x30-0x3F), intermediários (0x20-0x2F) e um caractere final (0x40-0x7E).
    private static int ParseCsi(string s, int j, ref bool altOn, ref bool altOff)
    {
        int paramStart = j;
        while (j < s.Length && s[j] >= '0' && s[j] <= '?')
        {
            j++;
        }
        int paramEnd = j;
        while (j < s.Length && s[j] >= ' ' && s[j] <= '/')
        {
            j++;
        }
        if (j >= s.Length)
        {
            return -1;
        }
        char final = s[j];
        if (final < '@' || final > '~')
        {
            return j; // malformada: descarta o começo e mantém o caractere atual
        }
        if ((final == 'h' || final == 'l') && IsAlternateScreenMode(s.AsSpan(paramStart, paramEnd - paramStart)))
        {
            if (final == 'h')
            {
                altOn = true;
            }
            else
            {
                altOff = true;
            }
        }
        return j + 1;
    }

    // ESC[?1049h, ESC[?1047h e ESC[?47h ligam a tela alternativa (programas de tela cheia).
    private static bool IsAlternateScreenMode(ReadOnlySpan<char> parameters)
    {
        if (parameters.Length < 2 || parameters[0] != '?')
        {
            return false;
        }
        foreach (var range in parameters[1..].Split(';'))
        {
            var p = parameters[1..][range];
            if (p.SequenceEqual("1049") || p.SequenceEqual("1047") || p.SequenceEqual("47"))
            {
                return true;
            }
        }
        return false;
    }

    // OSC/DCS: vão até BEL (0x07) ou ST (ESC \).
    private static int ParseString(string s, int j)
    {
        for (int k = j; k < s.Length; k++)
        {
            char c = s[k];
            if (c == '\u0007' || c == '\u009c')
            {
                return k + 1;
            }
            if (c == Esc)
            {
                if (k + 1 >= s.Length)
                {
                    return -1;
                }
                return s[k + 1] == '\\' ? k + 2 : k;
            }
        }
        return -1;
    }
}
