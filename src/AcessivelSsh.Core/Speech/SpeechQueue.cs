using System.Text;

namespace AcessivelSsh.Core.Speech;

/// <summary>Configuração da fila de fala. A fase 5 lê estes valores do arquivo de configurações.</summary>
/// <remarks>"init" permite definir o valor só na criação, como um dataclass(frozen=True).</remarks>
public sealed record SpeechQueueOptions
{
    /// <summary>Silêncio necessário antes de falar o que se acumulou.</summary>
    public TimeSpan Debounce { get; init; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Espera máxima com saída contínua, para a fala não atrasar indefinidamente.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Acima deste número de linhas num lote, fala só o resumo.</summary>
    public int SummaryLineThreshold { get; init; } = 30;

    /// <summary>Com saída grande e contínua, repete o resumo a cada intervalo.</summary>
    public TimeSpan SummaryMaxDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Intervalo mínimo entre duas falas de porcentagem de progresso.</summary>
    public TimeSpan ProgressInterval { get; init; } = TimeSpan.FromSeconds(5);

    // Func<int, string> é uma função int -> str, como Callable[[int], str] no Python.
    public Func<int, string> SummaryFormatter { get; init; } = lines => $"{lines} linhas recebidas";

    public Func<int, string> ProgressFormatter { get; init; } = percent => $"{percent} por cento";
}

/// <summary>
/// Agrupa os pedaços de saída e fala o lote depois de um curto silêncio (debounce).
/// Thread-safe: pode ser chamada da thread de leitura do SSH e da interface.
/// </summary>
/// <remarks>
/// O relógio vem de um TimeProvider. Nos testes, um relógio falso avança o tempo na hora,
/// sem esperar de verdade.
/// </remarks>
public sealed class SpeechQueue : IDisposable
{
    private static readonly TimeSpan MinTimerDelay = TimeSpan.FromMilliseconds(1);

    private readonly IScreenReader _reader;
    private SpeechQueueOptions _options;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private readonly object _lock = new();
    private readonly StringBuilder _buffer = new();
    private bool _pending;
    private bool _summarizing;
    private int _lineCount;
    private bool _trailingPartial;
    private long _batchStarted;
    private long _lastChunk;
    private long? _lastProgress;
    private bool _muted;
    private bool _autoRead = true;
    private bool _disposed;

    public SpeechQueue(IScreenReader reader, SpeechQueueOptions? options = null, TimeProvider? timeProvider = null)
    {
        _reader = reader;
        _options = options ?? new SpeechQueueOptions();
        _time = timeProvider ?? TimeProvider.System;
        _timer = _time.CreateTimer(_ => OnTimer(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Configuração atual. Trocar vale a partir do próximo lote de saída.</summary>
    public SpeechQueueOptions Options
    {
        get { lock (_lock) { return _options; } }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (_lock) { _options = value; }
        }
    }

    /// <summary>Leitura automática da saída. Desligada, só os avisos (SpeakNow) são falados.</summary>
    public bool AutoRead
    {
        get { lock (_lock) { return _autoRead; } }
        set { lock (_lock) { _autoRead = value; if (!value) { ClearPendingLocked(); } } }
    }

    /// <summary>Silêncio total, usado com o Windows bloqueado (recomendação do NVDA).</summary>
    public bool Muted
    {
        get { lock (_lock) { return _muted; } }
        set
        {
            lock (_lock)
            {
                _muted = value;
                if (value)
                {
                    ClearPendingLocked();
                }
            }
            if (value)
            {
                _reader.Cancel();
            }
        }
    }

    /// <summary>Acrescenta saída do servidor ao lote atual.</summary>
    public void Enqueue(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        lock (_lock)
        {
            if (_disposed || _muted || !_autoRead)
            {
                return;
            }
            long now = _time.GetTimestamp();
            if (!_pending)
            {
                _pending = true;
                _batchStarted = now;
            }
            _lastChunk = now;
            CountLines(text);
            if (!_summarizing)
            {
                _buffer.Append(text);
                if (TotalLines > _options.SummaryLineThreshold)
                {
                    // Saída grande: a partir daqui só contamos linhas, sem guardar o texto.
                    _summarizing = true;
                    _buffer.Clear();
                }
            }
            _timer.Change(ComputeDelayLocked(now), Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Fala um aviso na hora (estado da conexão, senha solicitada), mesmo com a leitura automática desligada.</summary>
    public void SpeakNow(string text, bool interrupt = true, bool discardPending = true)
    {
        lock (_lock)
        {
            if (_disposed || _muted)
            {
                return;
            }
            if (discardPending)
            {
                ClearPendingLocked();
            }
        }
        _reader.Speak(text, interrupt);
    }

    /// <summary>Fala a porcentagem de uma barra de progresso, no máximo uma vez a cada ProgressInterval.</summary>
    public void ReportProgress(int percent)
    {
        lock (_lock)
        {
            if (_disposed || _muted || !_autoRead)
            {
                return;
            }
            long now = _time.GetTimestamp();
            if (_lastProgress is long last && _time.GetElapsedTime(last, now) < _options.ProgressInterval)
            {
                return;
            }
            _lastProgress = now;
        }
        _reader.Speak(_options.ProgressFormatter(percent));
    }

    /// <summary>Descarta o que está na fila e interrompe a fala atual.</summary>
    public void Cancel()
    {
        lock (_lock)
        {
            ClearPendingLocked();
        }
        _reader.Cancel();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            ClearPendingLocked();
        }
        _timer.Dispose();
    }

    private int TotalLines => _lineCount + (_trailingPartial ? 1 : 0);

    private void CountLines(string text)
    {
        int lastNewLine = -1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                _lineCount++;
                lastNewLine = i;
            }
        }
        bool tailHasText = !text.AsSpan(lastNewLine + 1).IsWhiteSpace();
        _trailingPartial = lastNewLine >= 0 ? tailHasText : _trailingPartial || tailHasText;
    }

    // Fala quando houver Debounce de silêncio, ou quando o lote atingir a espera máxima.
    private TimeSpan ComputeDelayLocked(long now)
    {
        var maxDelay = _summarizing ? _options.SummaryMaxDelay : _options.MaxDelay;
        var untilQuiet = _options.Debounce - _time.GetElapsedTime(_lastChunk, now);
        var untilMax = maxDelay - _time.GetElapsedTime(_batchStarted, now);
        var delay = untilQuiet < untilMax ? untilQuiet : untilMax;
        return delay < MinTimerDelay ? MinTimerDelay : delay;
    }

    private void OnTimer()
    {
        string text;
        lock (_lock)
        {
            if (!_pending || _disposed || _muted)
            {
                return;
            }
            long now = _time.GetTimestamp();
            var delay = ComputeDelayLocked(now);
            if (delay > MinTimerDelay)
            {
                _timer.Change(delay, Timeout.InfiniteTimeSpan); // chegou mais saída: espera de novo
                return;
            }
            text = _summarizing ? _options.SummaryFormatter(TotalLines) : _buffer.ToString();
            ClearPendingLocked();
        }
        if (!string.IsNullOrWhiteSpace(text))
        {
            _reader.Speak(text);
        }
    }

    private void ClearPendingLocked()
    {
        _buffer.Clear();
        _pending = false;
        _summarizing = false;
        _lineCount = 0;
        _trailingPartial = false;
        if (!_disposed)
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }
}
