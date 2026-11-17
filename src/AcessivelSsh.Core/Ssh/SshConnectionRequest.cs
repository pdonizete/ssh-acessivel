namespace AcessivelSsh.Core.Ssh;

/// <summary>Dados para abrir uma conexão: por senha (Password) ou por chave (PrivateKeyPath).</summary>
/// <remarks>
/// "record" é parecido com um @dataclass(frozen=True) do Python. "init" deixa definir a propriedade
/// só na criação: new SshConnectionRequest(...) { Password = "x" }.
/// O ToString() gerado listaria todos os campos, inclusive a senha. Por isso ele é substituído abaixo.
/// </remarks>
public sealed record SshConnectionRequest(string Host, int Port, string Username)
{
    public string? Password { get; init; }

    /// <summary>Se definido, autentica pela chave privada em vez da senha.</summary>
    public string? PrivateKeyPath { get; init; }

    public string? Passphrase { get; init; }

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Intervalo do keep-alive. Timeout.InfiniteTimeSpan desliga.</summary>
    public TimeSpan KeepAliveInterval { get; init; } = TimeSpan.FromSeconds(30);

    public override string ToString() => $"{Username}@{Host}:{Port}";
}

/// <summary>Chave pública que o servidor apresentou, para o usuário conferir.</summary>
public sealed record HostKeyInfo(string Host, int Port, string KeyType, string FingerprintSha256)
{
    /// <summary>Impressão digital no formato do OpenSSH (SHA256:...).</summary>
    public string DisplayFingerprint => FingerprintSha256.StartsWith("SHA256:", StringComparison.Ordinal)
        ? FingerprintSha256
        : "SHA256:" + FingerprintSha256;
}
