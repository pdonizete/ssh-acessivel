using System.Threading.Channels;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace SshAcessivel.Core.Ssh;

/// <summary>Sessão de shell com SSH.NET: PTY com TERM=dumb, leitura e envio em segundo plano.</summary>
public sealed class SshSession : ISshSession
{
    private const string TerminalName = "dumb";
    // Muitas colunas para o servidor não quebrar linhas longas no meio.
    private const uint TerminalColumns = 200;
    private const uint TerminalRows = 50;
    private const int BufferSize = 16 * 1024;

    private readonly object _lock = new();
    private readonly List<object> _authMethods = [];
    private SshClient? _client;
    private ShellStream? _stream;
    private Channel<string>? _sendQueue;

    // Cada conexão recebe um número. Threads de uma conexão antiga comparam o número
    // e ficam quietas, em vez de anunciar "conexão caiu" para a conexão nova.
    private int _generation;
    private string _host = "";
    private int _port;
    private string _target = "";
    private volatile bool _hostKeyRejected;
    private volatile string? _lastError;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    public event EventHandler<byte[]>? DataReceived;

    public Func<HostKeyInfo, bool>? HostKeyValidator { get; set; }

    public async Task ConnectAsync(SshConnectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        int generation;
        lock (_lock)
        {
            if (State is ConnectionState.Connecting or ConnectionState.Connected)
            {
                throw new InvalidOperationException("Já existe uma conexão ativa.");
            }
            generation = ++_generation;
        }
        CloseResources();
        _host = request.Host;
        _port = request.Port;
        _target = request.ToString();
        _hostKeyRejected = false;
        _lastError = null;
        SetState(ConnectionState.Connecting, _target);

        ShellStream stream;
        try
        {
            // Dentro do try: uma chave que não abre vira estado de erro, não exceção.
            var methods = CreateAuthenticationMethods(request);
            var info = new ConnectionInfo(request.Host, request.Port, request.Username, methods)
            {
                Timeout = request.ConnectTimeout,
            };
            var client = new SshClient(info) { KeepAliveInterval = request.KeepAliveInterval };
            client.HostKeyReceived += OnHostKeyReceived;
            client.ErrorOccurred += OnClientError;
            lock (_lock)
            {
                _client = client;
            }

            // ConfigureAwait(false): o resto do método pode continuar em qualquer thread de fundo.
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            stream = client.CreateShellStream(TerminalName, TerminalColumns, TerminalRows, 0, 0, BufferSize);
        }
        catch (Exception ex)
        {
            if (generation != Volatile.Read(ref _generation))
            {
                return; // o usuário desconectou durante a tentativa; DisconnectAsync já avisou
            }
            CloseResources();
            var (state, detail) = ex switch
            {
                _ when _hostKeyRejected => (ConnectionState.HostKeyRejected, (string?)null),
                SshAuthenticationException => (ConnectionState.AuthenticationFailed, ex.Message),
                SshOperationTimeoutException => (ConnectionState.TimedOut, ex.Message),
                OperationCanceledException when cancellationToken.IsCancellationRequested => (ConnectionState.Disconnected, null),
                _ => (ConnectionState.Error, ex.Message),
            };
            SetState(state, detail);
            return;
        }

        var queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        lock (_lock)
        {
            if (generation != _generation)
            {
                stream.Dispose();
                return;
            }
            _stream = stream;
            _sendQueue = queue;
        }
        SetState(ConnectionState.Connected, _target);

        // ShellStream.Read bloqueia e não aceita CancellationToken, então usamos uma thread
        // dedicada, como threading.Thread(daemon=True) no Python. Ela termina quando o canal fecha.
        var reader = new Thread(() => ReadLoop(stream, generation)) { IsBackground = true, Name = "SSH leitura" };
        reader.Start();
        _ = Task.Run(() => WriteLoopAsync(stream, queue.Reader));
    }

    // Monta os métodos de autenticação. Objetos com recursos (a chave carregada) vão para
    // _authMethods, para serem liberados em CloseResources.
    private AuthenticationMethod[] CreateAuthenticationMethods(SshConnectionRequest request)
    {
        if (!string.IsNullOrEmpty(request.PrivateKeyPath))
        {
            var keyFile = string.IsNullOrEmpty(request.Passphrase)
                ? new PrivateKeyFile(request.PrivateKeyPath)
                : new PrivateKeyFile(request.PrivateKeyPath, request.Passphrase);
            var keyAuth = new PrivateKeyAuthenticationMethod(request.Username, keyFile);
            lock (_lock)
            {
                _authMethods.Add(keyFile);
                _authMethods.Add(keyAuth);
            }
            return [keyAuth];
        }

        // Muitos servidores Linux só aceitam "keyboard-interactive" em vez de "password". Oferecemos os dois.
        var secret = request.Password ?? "";
        var password = new PasswordAuthenticationMethod(request.Username, secret);
        var keyboard = new KeyboardInteractiveAuthenticationMethod(request.Username);
        keyboard.AuthenticationPrompt += (_, e) =>
        {
            foreach (var prompt in e.Prompts)
            {
                prompt.Response = secret;
            }
        };
        lock (_lock)
        {
            _authMethods.Add(password);
            _authMethods.Add(keyboard);
        }
        return [password, keyboard];
    }

    public bool SendLine(string line) => Enqueue(line + "\r"); // Enter num PTY é \r

    public bool SendRaw(string text) => Enqueue(text);

    public async Task DisconnectAsync()
    {
        bool wasActive;
        lock (_lock)
        {
            wasActive = State is ConnectionState.Connecting or ConnectionState.Connected;
            _generation++;
        }
        await Task.Run(CloseResources).ConfigureAwait(false);
        if (wasActive)
        {
            SetState(ConnectionState.Disconnected, null);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _generation++;
        }
        CloseResources();
    }

    private bool Enqueue(string text)
    {
        var queue = _sendQueue;
        return State == ConnectionState.Connected && queue is not null && queue.Writer.TryWrite(text);
    }

    private void ReadLoop(ShellStream stream, int generation)
    {
        var buffer = new byte[BufferSize];
        try
        {
            while (true)
            {
                // Retorna 0 quando o canal fecha (o SSH.NET não lança exceção nesse caso).
                int count = stream.Read(buffer, 0, buffer.Length);
                if (count == 0)
                {
                    break;
                }
                if (generation != Volatile.Read(ref _generation))
                {
                    return;
                }
                DataReceived?.Invoke(this, buffer.AsSpan(0, count).ToArray());
            }
        }
        catch (Exception ex)
        {
            _lastError ??= ex.Message;
        }
        OnConnectionEnded(generation);
    }

    private static async Task WriteLoopAsync(ShellStream stream, ChannelReader<string> reader)
    {
        try
        {
            // "await foreach" é como "async for" do Python: espera cada item da fila.
            await foreach (var text in reader.ReadAllAsync().ConfigureAwait(false))
            {
                stream.Write(text); // Write(string) já envia (flush)
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or SshException or IOException)
        {
            // A conexão fechou; o ReadLoop anuncia isso.
        }
    }

    private void OnConnectionEnded(int generation)
    {
        lock (_lock)
        {
            if (generation != _generation)
            {
                return; // desconexão pedida pelo usuário ou nova conexão em andamento
            }
            _generation++;
        }
        var detail = _lastError;
        CloseResources();
        SetState(ConnectionState.ConnectionLost, detail);
    }

    private void OnHostKeyReceived(object? sender, HostKeyEventArgs e)
    {
        bool trusted;
        try
        {
            var validator = HostKeyValidator;
            trusted = validator is not null
                      && validator(new HostKeyInfo(_host, _port, e.HostKeyName, e.FingerPrintSHA256));
        }
        catch (Exception)
        {
            trusted = false; // na dúvida, não confiar
        }
        e.CanTrust = trusted;
        if (!trusted)
        {
            _hostKeyRejected = true;
        }
    }

    private void OnClientError(object? sender, ExceptionEventArgs e) => _lastError = e.Exception.Message;

    private void SetState(ConnectionState state, string? detail)
    {
        lock (_lock)
        {
            State = state;
        }
        StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(state, detail));
    }

    private void CloseResources()
    {
        SshClient? client;
        ShellStream? stream;
        Channel<string>? queue;
        object[] authMethods;
        lock (_lock)
        {
            client = _client;
            stream = _stream;
            queue = _sendQueue;
            authMethods = [.. _authMethods];
            _client = null;
            _stream = null;
            _sendQueue = null;
            _authMethods.Clear();
        }

        queue?.Writer.TryComplete();
        if (stream is not null)
        {
            TryRun(stream.Dispose); // acorda a thread de leitura, que recebe 0
        }
        if (client is not null)
        {
            client.HostKeyReceived -= OnHostKeyReceived;
            client.ErrorOccurred -= OnClientError;
            TryRun(client.Disconnect);
            TryRun(client.Dispose);
        }
        foreach (var method in authMethods)
        {
            if (method is IDisposable disposable)
            {
                TryRun(disposable.Dispose);
            }
        }
    }

    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            // Falhas ao fechar uma conexão que já caiu não interessam ao usuário.
        }
    }
}
