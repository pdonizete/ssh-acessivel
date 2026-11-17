using SshAcessivel.Core.Speech;

namespace SshAcessivel.Tests.Speech;

// A pasta de saída dos testes não recebe a DLL do NVDA (só o App a copia).
// Assim estes testes verificam o caminho "DLL ausente": nada pode lançar exceção.
public class NvdaScreenReaderTests
{
    [Fact]
    public void Initialize_SemDll_RetornaLibraryNotFound()
    {
        var reader = new NvdaScreenReader();

        Assert.Equal(ScreenReaderStatus.LibraryNotFound, reader.Initialize());
        Assert.Null(reader.LoadedLibraryPath);
    }

    [Fact]
    public void Chamadas_SemDll_RetornamFalseSemExcecao()
    {
        var reader = new NvdaScreenReader();
        reader.Initialize();

        Assert.False(reader.Speak("olá, ação", interrupt: true));
        Assert.False(reader.Braille("teste"));
        Assert.False(reader.Cancel());
    }

    [Fact]
    public void Speak_TextoVazio_NaoChamaDllERetornaTrue()
    {
        var reader = new NvdaScreenReader();

        Assert.True(reader.Speak(""));
    }

    [Fact]
    public void NullScreenReader_EstaDesativado()
    {
        IScreenReader reader = new NullScreenReader();

        Assert.Equal(ScreenReaderStatus.Disabled, reader.Initialize());
        Assert.False(reader.Speak("x"));
    }
}
