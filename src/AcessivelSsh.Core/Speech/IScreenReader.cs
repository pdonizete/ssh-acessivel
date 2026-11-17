namespace AcessivelSsh.Core.Speech;

/// <summary>
/// Contrato de qualquer leitor de tela que o app sabe usar.
/// </summary>
/// <remarks>
/// Uma interface em C# é parecida com um typing.Protocol ou uma classe abstrata
/// (abc.ABC) do Python, só com métodos abstratos: ela diz QUAIS métodos existem,
/// sem implementá-los. A diferença é que o compilador verifica: uma classe que
/// declara ": IScreenReader" e esquece um método não compila.
/// O resto do app só conhece IScreenReader. Assim dá para trocar o NVDA por
/// outro leitor no futuro, ou por um mock nos testes.
/// </remarks>
public interface IScreenReader
{
    /// <summary>Estado atual, definido por <see cref="Initialize"/>.</summary>
    ScreenReaderStatus Status { get; }

    /// <summary>Carrega a biblioteca e verifica se o leitor está rodando. Nunca lança exceção.</summary>
    ScreenReaderStatus Initialize();

    /// <summary>Fala o texto. Retorna false se o leitor não respondeu. Nunca lança exceção.</summary>
    /// <param name="interrupt">Se true, cancela a fala atual antes de falar.</param>
    bool Speak(string text, bool interrupt = false);

    /// <summary>Mostra uma mensagem rápida na linha braille. Nunca lança exceção.</summary>
    bool Braille(string text);

    /// <summary>Interrompe a fala atual. Nunca lança exceção.</summary>
    bool Cancel();
}
