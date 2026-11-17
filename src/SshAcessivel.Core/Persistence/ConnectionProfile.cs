using System.Text.Json.Serialization;

namespace SshAcessivel.Core.Persistence;

public enum AuthenticationKind
{
    Password,
    PrivateKey,
}

/// <summary>Uma conexão salva no gerenciador.</summary>
public sealed class ConnectionProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    public string Host { get; set; } = "";

    public int Port { get; set; } = 22;

    public string Username { get; set; } = "";

    public AuthenticationKind Authentication { get; set; } = AuthenticationKind.Password;

    public string? PrivateKeyPath { get; set; }

    public string? Group { get; set; }

    /// <summary>
    /// Senha (autenticação por senha) ou passphrase (por chave), cifrada com DPAPI em base64.
    /// Null quando o usuário não quis salvar.
    /// </summary>
    public string? ProtectedSecret { get; set; }

    // [JsonIgnore]: propriedade calculada, não vai para o arquivo.
    [JsonIgnore]
    public bool HasSavedSecret => !string.IsNullOrEmpty(ProtectedSecret);

    // MemberwiseClone copia todos os campos, como copy.copy() no Python.
    public ConnectionProfile Clone() => (ConnectionProfile)MemberwiseClone();

    public override string ToString() => $"{Name} ({Username}@{Host}:{Port})";
}
