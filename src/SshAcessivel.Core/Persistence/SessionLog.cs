using System.Text;

namespace SshAcessivel.Core.Persistence;

/// <summary>
/// Log opcional da sessão, em .txt. Recebe só o texto exibido no campo de saída.
/// Senhas digitadas não entram: o servidor não as ecoa e o campo de senha não é registrado.
/// </summary>
/// <remarks>Write é chamado pela thread de leitura do SSH; Dispose, pela interface. Um lock protege o arquivo.</remarks>
public sealed class SessionLog : IDisposable
{
    private readonly object _lock = new();
    private StreamWriter? _writer;

    public SessionLog(string path)
    {
        FilePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        _writer = new StreamWriter(FilePath, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
    }

    public string FilePath { get; }

    /// <summary>A gravação falhou (disco cheio, arquivo removido). O log parou.</summary>
    public bool Failed { get; private set; }

    public void Write(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        lock (_lock)
        {
            if (_writer is null)
            {
                return;
            }
            try
            {
                _writer.Write(text);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                Failed = true;
                AppLog.Error("Falha ao gravar o log da sessão", ex);
                CloseWriter();
            }
        }
    }

    public void WriteLine(string text) => Write(text + "\r\n");

    public void Dispose()
    {
        lock (_lock)
        {
            CloseWriter();
        }
    }

    private void CloseWriter()
    {
        try
        {
            _writer?.Dispose();
        }
        catch (IOException)
        {
            // O arquivo já estava com problema.
        }
        _writer = null;
    }
}
