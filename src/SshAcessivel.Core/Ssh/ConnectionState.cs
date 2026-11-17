namespace SshAcessivel.Core.Ssh;

/// <summary>Estados da conexão. Toda mudança é mostrada na barra de status e falada.</summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    AuthenticationFailed,
    HostKeyRejected,
    TimedOut,
    Error,
    ConnectionLost,
}

/// <summary>Dados do evento StateChanged.</summary>
/// <remarks>
/// "(ConnectionState state, string? detail)" logo após o nome da classe é um construtor primário:
/// equivale a um __init__(self, state, detail) que guarda os dois valores.
/// </remarks>
public sealed class ConnectionStateChangedEventArgs(ConnectionState state, string? detail) : EventArgs
{
    public ConnectionState State { get; } = state;

    /// <summary>Destino (usuario@host:porta) ou mensagem de erro, conforme o estado.</summary>
    public string? Detail { get; } = detail;
}
