using AcessivelSsh.Core.Speech;

namespace AcessivelSsh.Tests.Speech;

/// <summary>Mock de IScreenReader: guarda o que seria falado, sem chamar o NVDA.</summary>
internal sealed class FakeScreenReader : IScreenReader
{
    private readonly object _lock = new();
    private readonly List<string> _spoken = [];

    public int CancelCount { get; private set; }

    public IReadOnlyList<string> Spoken
    {
        get { lock (_lock) { return [.. _spoken]; } }
    }

    public ScreenReaderStatus Status => ScreenReaderStatus.Ready;

    public ScreenReaderStatus Initialize() => ScreenReaderStatus.Ready;

    public bool Speak(string text, bool interrupt = false)
    {
        lock (_lock) { _spoken.Add(text); }
        return true;
    }

    public bool Braille(string text) => true;

    public bool Cancel()
    {
        lock (_lock) { CancelCount++; }
        return true;
    }
}
