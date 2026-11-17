namespace SshAcessivel.Core.Speech;

/// <summary>Resultado da inicialização de um leitor de tela.</summary>
public enum ScreenReaderStatus
{
    /// <summary>Biblioteca carregada e leitor de tela respondendo.</summary>
    Ready,

    /// <summary>Biblioteca carregada, mas o leitor de tela não está em execução agora.
    /// As chamadas continuam sendo tentadas: se o NVDA for aberto depois, a fala volta sozinha.</summary>
    NotRunning,

    /// <summary>A DLL do leitor de tela não foi encontrada ou não pôde ser carregada.</summary>
    LibraryNotFound,

    /// <summary>Leitor de tela desativado de propósito (NullScreenReader, mocks).</summary>
    Disabled,
}
