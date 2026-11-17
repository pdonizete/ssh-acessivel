namespace SshAcessivel.Core.Ssh;

/// <summary>
/// Uma sessão de shell remoto. A interface só conhece este contrato; abas, SFTP e
/// port forwarding podem ser adicionados depois sem mudar a UI atual.
/// </summary>
/// <remarks>
/// Os eventos são disparados em threads de fundo. Quem atualiza a interface precisa
/// repassar a chamada para a thread da UI (BeginInvoke).
/// "event" em C# é uma lista de callbacks: "+=" inscreve uma função, "-=" remove.
/// </remarks>
public interface ISshSession : IDisposable
{
    ConnectionState State { get; }

    event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>Bytes brutos recebidos do servidor (ainda não decodificados).</summary>
    event EventHandler<byte[]>? DataReceived;

    /// <summary>
    /// Chamado durante a conexão para decidir se a chave do servidor é confiável.
    /// Sem validador, a conexão é recusada (padrão seguro).
    /// </summary>
    Func<HostKeyInfo, bool>? HostKeyValidator { get; set; }

    /// <summary>Conecta e abre o shell. Falhas esperadas viram estados, não exceções.</summary>
    Task ConnectAsync(SshConnectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Envia uma linha (com Enter). Não faz I/O na thread de quem chama. False se não conectado.</summary>
    bool SendLine(string line);

    /// <summary>Envia texto sem Enter (ex.: Ctrl+C = "\x03"). False se não conectado.</summary>
    bool SendRaw(string text);

    Task DisconnectAsync();
}
