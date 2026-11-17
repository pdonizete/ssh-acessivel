using System.Text.Json;
using System.Text.Json.Serialization;

namespace AcessivelSsh.Core.Persistence;

/// <summary>Formato do arquivo connections.json e dos arquivos exportados.</summary>
internal sealed class ConnectionsFileModel
{
    public int Version { get; set; } = 1;

    public List<ConnectionProfile?>? Connections { get; set; }
}

/// <summary>Lista de conexões salva em JSON.</summary>
public sealed class ConnectionStore
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        // Grava "Password"/"PrivateKey" em vez de 0/1: o arquivo fica legível.
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly List<ConnectionProfile> _profiles = [];
    // true quando o arquivo existente não pôde ser lido nem copiado: não salvamos por cima dele.
    private bool _saveBlocked;

    public ConnectionStore(string path) => _path = path;

    public IReadOnlyList<ConnectionProfile> Profiles => _profiles;

    /// <summary>O arquivo existia, mas não pôde ser lido.</summary>
    public bool LoadFailed { get; private set; }

    /// <summary>Cópia do arquivo ilegível, guardada antes de começar com a lista vazia.</summary>
    public string? BackupPath { get; private set; }

    public void Load()
    {
        _profiles.Clear();
        LoadFailed = false;
        BackupPath = null;
        _saveBlocked = false;
        if (!File.Exists(_path))
        {
            return;
        }
        try
        {
            var data = JsonSerializer.Deserialize<ConnectionsFileModel>(File.ReadAllText(_path), JsonOptions);
            _profiles.AddRange(Sanitize(data?.Connections));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LoadFailed = true;
            var backup = $"{_path}.corrompido-{DateTime.Now:yyyyMMdd-HHmmss}";
            try
            {
                File.Copy(_path, backup, overwrite: false);
                BackupPath = backup;
            }
            catch (Exception copyEx) when (copyEx is IOException or UnauthorizedAccessException)
            {
                _saveBlocked = true;
            }
        }
    }

    public void Save()
    {
        if (_saveBlocked)
        {
            throw new InvalidOperationException("O arquivo de conexões não pôde ser lido e não será sobrescrito.");
        }
        var model = new ConnectionsFileModel { Connections = [.. _profiles] };
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(model, JsonOptions));
    }

    public void AddOrUpdate(ConnectionProfile profile)
    {
        int index = _profiles.FindIndex(p => p.Id == profile.Id);
        if (index >= 0)
        {
            _profiles[index] = profile;
        }
        else
        {
            _profiles.Add(profile);
        }
    }

    public bool Remove(Guid id) => _profiles.RemoveAll(p => p.Id == id) > 0;

    /// <summary>Exporta a lista sem nenhum segredo.</summary>
    public void Export(string path)
    {
        var copies = _profiles.Select(p =>
        {
            var copy = p.Clone();
            copy.ProtectedSecret = null;
            return (ConnectionProfile?)copy;
        }).ToList();
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(new ConnectionsFileModel { Connections = copies }, JsonOptions));
    }

    /// <summary>Importa conexões de um arquivo exportado. Ignora segredos e conexões repetidas. Retorna quantas entraram.</summary>
    /// <exception cref="JsonException">Arquivo inválido.</exception>
    public int Import(string path)
    {
        var data = JsonSerializer.Deserialize<ConnectionsFileModel>(File.ReadAllText(path), JsonOptions)
                   ?? throw new JsonException("Arquivo vazio.");
        int added = 0;
        foreach (var profile in Sanitize(data.Connections))
        {
            if (_profiles.Any(p => SameTarget(p, profile)))
            {
                continue;
            }
            profile.Id = Guid.NewGuid();
            profile.ProtectedSecret = null; // segredos cifrados de outro usuário não servem aqui
            _profiles.Add(profile);
            added++;
        }
        return added;
    }

    // "yield return" funciona como no Python: a função vira um gerador.
    private static IEnumerable<ConnectionProfile> Sanitize(IEnumerable<ConnectionProfile?>? profiles)
    {
        foreach (var p in profiles ?? Enumerable.Empty<ConnectionProfile?>())
        {
            if (p is null || string.IsNullOrWhiteSpace(p.Host) || string.IsNullOrWhiteSpace(p.Username) || p.Port is < 1 or > 65535)
            {
                continue;
            }
            p.Host = p.Host.Trim();
            p.Username = p.Username.Trim();
            if (string.IsNullOrWhiteSpace(p.Name))
            {
                p.Name = p.Host;
            }
            if (p.Id == Guid.Empty)
            {
                p.Id = Guid.NewGuid();
            }
            yield return p;
        }
    }

    private static bool SameTarget(ConnectionProfile a, ConnectionProfile b) =>
        string.Equals(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase)
        && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
        && a.Port == b.Port
        && string.Equals(a.Username, b.Username, StringComparison.Ordinal);
}
