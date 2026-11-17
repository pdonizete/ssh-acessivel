namespace SshAcessivel.Core.Speech;

/// <summary>
/// Leitor de tela que não faz nada. Usado quando a fala está desligada
/// e nos testes que não precisam verificar o que foi falado.
/// </summary>
public sealed class NullScreenReader : IScreenReader
{
    public ScreenReaderStatus Status => ScreenReaderStatus.Disabled;

    public ScreenReaderStatus Initialize() => ScreenReaderStatus.Disabled;

    public bool Speak(string text, bool interrupt = false) => false;

    public bool Braille(string text) => false;

    public bool Cancel() => false;
}
