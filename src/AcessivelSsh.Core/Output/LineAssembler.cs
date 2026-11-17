using System.Text;

namespace AcessivelSsh.Core.Output;

/// <summary>Uma linha terminada por \n.</summary>
/// <param name="WasOverwritten">A linha foi reescrita com \r (barra de progresso) antes de terminar.</param>
public readonly record struct CompletedLine(string Text, bool WasOverwritten);

public readonly record struct LineAssemblerResult(IReadOnlyList<CompletedLine> CompletedLines, bool PartialOverwritten);

/// <summary>
/// Monta linhas a partir do texto do terminal, tratando \r, \b e caracteres de controle.
/// </summary>
/// <remarks>
/// \r sem \n volta ao início da linha. Se depois vier texto, o programa está reescrevendo
/// a linha (apt, wget, pip): guardamos só a versão mais nova. \r\n é uma quebra de linha normal.
/// </remarks>
public sealed class LineAssembler
{
    private readonly StringBuilder _line = new();
    private int _column;
    private bool _afterCarriageReturn;
    private bool _lineOverwritten;
    private bool _partialOverwrittenInCall;

    /// <summary>Linha atual, ainda sem \n (ex.: o prompt do shell).</summary>
    public string Partial => _line.ToString();

    public LineAssemblerResult Process(string text)
    {
        List<CompletedLine>? completed = null;
        _partialOverwrittenInCall = false;
        foreach (var c in text)
        {
            switch (c)
            {
                case '\n':
                    (completed ??= []).Add(new CompletedLine(_line.ToString(), _lineOverwritten));
                    _line.Clear();
                    _column = 0;
                    _afterCarriageReturn = false;
                    _lineOverwritten = false;
                    _partialOverwrittenInCall = false;
                    break;
                case '\r':
                    _column = 0;
                    _afterCarriageReturn = true;
                    break;
                case '\b':
                    if (_column > 0)
                    {
                        _column--;
                    }
                    break;
                case '\t':
                    Write(c);
                    break;
                case < ' ' or '\x7f' or (>= '\x80' and <= '\x9f'):
                    break; // campainha e outros controles: ignorados
                default:
                    Write(c);
                    break;
            }
        }
        return new LineAssemblerResult((IReadOnlyList<CompletedLine>?)completed ?? Array.Empty<CompletedLine>(), _partialOverwrittenInCall);
    }

    public void Reset()
    {
        _line.Clear();
        _column = 0;
        _afterCarriageReturn = false;
        _lineOverwritten = false;
        _partialOverwrittenInCall = false;
    }

    private void Write(char c)
    {
        if (_afterCarriageReturn)
        {
            _afterCarriageReturn = false;
            if (_column == 0 && _line.Length > 0)
            {
                _line.Clear();
                _lineOverwritten = true;
                _partialOverwrittenInCall = true;
            }
        }
        // Depois de \b, o caractere novo substitui o antigo, como no terminal.
        if (_column < _line.Length)
        {
            _line[_column] = c;
        }
        else
        {
            _line.Append(c);
        }
        _column++;
    }
}
