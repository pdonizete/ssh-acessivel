using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SshAcessivel.Core.Persistence;

internal sealed class HistoryFileModel
{
    public string? Server { get; set; }

    public List<string?>? Commands { get; set; }
}

/// <summary>Histórico de comandos de um servidor, navegado com seta acima e seta abaixo.</summary>
/// <remarks>
/// Usado só pela thread da interface. SaveAsync tira uma cópia e grava o arquivo numa thread de fundo.
/// Senhas nunca chegam aqui: o campo de comando em modo senha não chama Add.
/// </remarks>
public sealed class CommandHistory
{
    // SemaphoreSlim(1, 1) é um lock que funciona com await, como asyncio.Lock no Python.
    private static readonly SemaphoreSlim s_saveLock = new(1, 1);

    private readonly List<string> _entries = [];
    // Posição na navegação. _entries.Count = "depois do último", ou seja, fora do histórico.
    private int _index;
    // O que estava digitado antes de começar a navegar; volta ao passar do último item.
    private string _draft = "";

    public CommandHistory(int maxEntries) => MaxEntries = Math.Max(1, maxEntries);

    public int MaxEntries { get; private set; }

    public IReadOnlyList<string> Entries => _entries;

    public void SetMaxEntries(int maxEntries)
    {
        MaxEntries = Math.Max(1, maxEntries);
        Trim();
        ResetNavigation();
    }

    /// <summary>Guarda o comando, sem repetir o anterior, e volta ao fim da lista.</summary>
    public void Add(string command)
    {
        if (!string.IsNullOrWhiteSpace(command) && (_entries.Count == 0 || _entries[^1] != command))
        {
            _entries.Add(command);
            Trim();
        }
        ResetNavigation();
    }

    /// <summary>Comando anterior. No primeiro item, repete o primeiro. Null se o histórico estiver vazio.</summary>
    public string? Previous(string currentText)
    {
        if (_entries.Count == 0)
        {
            return null;
        }
        if (_index >= _entries.Count)
        {
            _draft = currentText;
            _index = _entries.Count;
        }
        if (_index > 0)
        {
            _index--;
        }
        return _entries[_index];
    }

    /// <summary>Próximo comando. Depois do último, devolve o texto que estava digitado. Null se já estiver no fim.</summary>
    public string? Next()
    {
        if (_index >= _entries.Count)
        {
            return null;
        }
        _index++;
        return _index == _entries.Count ? _draft : _entries[_index];
    }

    public void ResetNavigation()
    {
        _index = _entries.Count;
        _draft = "";
    }

    /// <summary>Arquivo do histórico de um servidor. O nome é um hash, para não depender de caracteres do host.</summary>
    public static string FileFor(string directory, string host, int port, string username)
    {
        var id = $"{username}@{host.Trim().ToLowerInvariant()}:{port}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))[..16].ToLowerInvariant();
        return Path.Combine(directory, hash + ".json");
    }

    public static CommandHistory Load(string path, int maxEntries)
    {
        var history = new CommandHistory(maxEntries);
        try
        {
            if (File.Exists(path))
            {
                var model = JsonSerializer.Deserialize<HistoryFileModel>(File.ReadAllText(path), ConnectionStore.JsonOptions);
                if (model?.Commands is { } commands)
                {
                    foreach (var command in commands)
                    {
                        if (!string.IsNullOrWhiteSpace(command))
                        {
                            history._entries.Add(command);
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            AppLog.Error("Falha ao ler o histórico " + path, ex);
        }
        history.Trim();
        history.ResetNavigation();
        return history;
    }

    public async Task SaveAsync(string path, string server)
    {
        // A cópia é feita aqui, na thread de quem chama; só a gravação vai para o fundo.
        var json = JsonSerializer.Serialize(new HistoryFileModel { Server = server, Commands = [.. _entries] }, ConnectionStore.JsonOptions);
        await s_saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() => AtomicFile.WriteAllText(path, json)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Falha ao salvar o histórico " + path, ex);
        }
        finally
        {
            s_saveLock.Release();
        }
    }

    private void Trim()
    {
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }
    }
}
