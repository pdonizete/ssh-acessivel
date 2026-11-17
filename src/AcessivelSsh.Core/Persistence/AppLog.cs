using System.Text;

namespace AcessivelSsh.Core.Persistence;

/// <summary>
/// Log de erros do app em %APPDATA%\AcessivelSsh\logs\, um arquivo por dia.
/// Nunca lança exceção. Nunca passe senhas ou passphrases para cá.
/// </summary>
public static class AppLog
{
    private static readonly object s_lock = new();

    public static string LogsDirectory => AppPaths.LogsDirectory;

    public static void Error(string message, Exception? exception = null) => Write("ERRO", message, exception);

    public static void Info(string message) => Write("INFO", message, null);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var sb = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [").Append(level).Append("] ")
                .Append(message)
                .Append(Environment.NewLine);
            if (exception is not null)
            {
                sb.Append(exception).Append(Environment.NewLine);
            }
            lock (s_lock)
            {
                Directory.CreateDirectory(LogsDirectory);
                var file = Path.Combine(LogsDirectory, $"erros-{DateTime.Now:yyyy-MM-dd}.txt");
                File.AppendAllText(file, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch (Exception)
        {
            // Se nem o log funciona, não há onde registrar. O app continua.
        }
    }
}
