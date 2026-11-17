namespace AcessivelSsh.Core.Persistence;

public enum HostKeyCheck
{
    /// <summary>Nenhuma chave salva para este host e porta.</summary>
    Unknown,

    /// <summary>A chave recebida é uma das salvas.</summary>
    Trusted,

    /// <summary>Há chave salva para este host e porta, mas a recebida é diferente. Bloquear.</summary>
    Mismatch,
}

/// <summary>
/// Arquivo known_hosts próprio do app. Uma linha por chave: "[host]:porta tipo SHA256:...".
/// </summary>
public sealed class KnownHostsStore
{
    private readonly object _lock = new();

    public KnownHostsStore(string path) => FilePath = path;

    public string FilePath { get; }

    /// <remarks>
    /// Qualquer chave salva para o host e porta com impressão digital diferente conta como Mismatch,
    /// mesmo que o tipo de chave seja outro. Assim um servidor falso não escapa do bloqueio
    /// apresentando um tipo de chave diferente.
    /// </remarks>
    public HostKeyCheck Check(string host, int port, string fingerprint)
    {
        var target = HostId(host, port);
        bool hasEntry = false;
        foreach (var (entryHost, _, entryFingerprint) in ReadEntries())
        {
            if (entryHost != target)
            {
                continue;
            }
            hasEntry = true;
            if (string.Equals(entryFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return HostKeyCheck.Trusted;
            }
        }
        return hasEntry ? HostKeyCheck.Mismatch : HostKeyCheck.Unknown;
    }

    public void Add(string host, int port, string keyType, string fingerprint)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
            File.AppendAllText(FilePath, $"{HostId(host, port)} {keyType} {fingerprint}\n");
        }
    }

    private static string HostId(string host, int port) => $"[{host.Trim().ToLowerInvariant()}]:{port}";

    private List<(string Host, string KeyType, string Fingerprint)> ReadEntries()
    {
        lock (_lock)
        {
            var entries = new List<(string, string, string)>();
            if (!File.Exists(FilePath))
            {
                return entries;
            }
            foreach (var line in File.ReadAllLines(FilePath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    continue;
                }
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    entries.Add((parts[0].ToLowerInvariant(), parts[1], parts[2]));
                }
            }
            return entries;
        }
    }
}
