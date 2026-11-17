using System.Text;

namespace AcessivelSsh.Core.Persistence;

/// <summary>Arquivos de dados do app em %APPDATA%\AcessivelSsh\.</summary>
public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AcessivelSsh");

    public static string ConnectionsFile => Path.Combine(DataDirectory, "connections.json");

    public static string KnownHostsFile => Path.Combine(DataDirectory, "known_hosts");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string HistoryDirectory => Path.Combine(DataDirectory, "historico");

    public static string LogsDirectory => Path.Combine(DataDirectory, "logs");
}

/// <summary>Grava um arquivo de forma segura: escreve num temporário e troca no fim.</summary>
/// <remarks>Se o app fechar no meio da gravação, o arquivo antigo continua inteiro.</remarks>
internal static class AtomicFile
{
    public static void WriteAllText(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temp = fullPath + ".tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, fullPath, overwrite: true);
    }
}
