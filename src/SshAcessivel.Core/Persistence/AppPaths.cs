using System.Text;

namespace SshAcessivel.Core.Persistence;

/// <summary>Arquivos de dados do app em %APPDATA%\ssh-acessivel\.</summary>
public static class AppPaths
{
    private static readonly string s_appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public static string DataDirectory { get; } = Path.Combine(s_appData, "ssh-acessivel");

    /// <summary>Pasta usada antes da troca de nome do app (AcessivelSsh).</summary>
    public static string LegacyDataDirectory { get; } = Path.Combine(s_appData, "AcessivelSsh");

    public static string ConnectionsFile => Path.Combine(DataDirectory, "connections.json");

    public static string KnownHostsFile => Path.Combine(DataDirectory, "known_hosts");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string HistoryDirectory => Path.Combine(DataDirectory, "historico");

    public static string LogsDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>
    /// Copia a pasta de dados antiga para a nova, só se a nova ainda não existir.
    /// A pasta antiga não é apagada. Nunca lança exceção. Retorna true se copiou.
    /// </summary>
    /// <remarks>
    /// A cópia vai primeiro para uma pasta temporária e só no fim é renomeada. Se o app fechar
    /// no meio, a pasta nova não fica pela metade e a cópia é refeita na próxima abertura.
    /// </remarks>
    public static bool MigrateLegacyData()
    {
        try
        {
            if (Directory.Exists(DataDirectory) || !Directory.Exists(LegacyDataDirectory))
            {
                return false;
            }
            var temp = DataDirectory + ".migrando";
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true); // sobra de uma cópia interrompida
            }
            CopyDirectory(LegacyDataDirectory, temp);
            Directory.Move(temp, DataDirectory);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
    }
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
