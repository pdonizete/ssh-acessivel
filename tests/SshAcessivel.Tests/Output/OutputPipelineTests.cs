using SshAcessivel.Core.Output;

namespace SshAcessivel.Tests.Output;

public class OutputPipelineTests
{
    [Fact]
    public void Utf8_CaractereDivididoEntrePacotes_SaiInteiro()
    {
        var pipeline = new OutputPipeline();

        // "ç" = C3 A7. O primeiro pacote termina no meio do caractere.
        Assert.Equal("a", pipeline.Process([0x61, 0xC3]).Partial);
        Assert.Equal("açb", pipeline.Process([0xA7, 0x62]).Partial);
    }

    [Fact]
    public void Utf8_AcentosEmPortugues()
    {
        var pipeline = new OutputPipeline();

        Assert.Equal("ação, coração, você\r\n", pipeline.Process("ação, coração, você\n"u8).Committed);
    }

    [Fact]
    public void CrLfDivididoEntrePacotes_ViraUmaQuebra()
    {
        var pipeline = new OutputPipeline();

        Assert.Equal("a", pipeline.Process("a\r"u8).Partial);
        var result = pipeline.Process("\nb"u8);
        Assert.Equal("a\r\n", result.Committed);
        Assert.Equal("b", result.Partial);
    }

    [Fact]
    public void Ansi_RemovidoInclusiveDivididoEntrePacotes()
    {
        var pipeline = new OutputPipeline();

        Assert.Equal("vermelho\r\n", pipeline.Process("\u001b[31mvermelho\u001b[0m\n"u8).Committed);
        Assert.Equal("ok", pipeline.Process("ok\u001b["u8).Partial);
        Assert.Equal("ok!\r\n", pipeline.Process("0m!\n"u8).Committed);
    }

    [Fact]
    public void EcoDoComando_NaoEFalado()
    {
        var pipeline = new OutputPipeline();
        pipeline.NotifyCommandSent("ls");

        var result = pipeline.Process("ls\r\narquivo1\r\n$ "u8);

        Assert.Equal("ls\r\narquivo1\r\n", result.Committed); // o eco continua na tela
        Assert.StartsWith("arquivo1", result.Speech);
        Assert.DoesNotContain("ls", result.Speech);
    }

    [Fact]
    public void EcoAposPrompt_SoOTextoNovoEFalado()
    {
        var pipeline = new OutputPipeline();
        Assert.Equal("$ ", pipeline.Process("$ "u8).Speech);

        pipeline.NotifyCommandSent("pwd");
        Assert.Equal("", pipeline.Process("pw"u8).Speech); // eco chegando aos poucos
        var result = pipeline.Process("d\r\n/home/paulo\r\n"u8);

        Assert.Equal("/home/paulo\r\n", result.Speech);
    }

    [Fact]
    public void BarraDeProgresso_NaoFalaCadaAtualizacao()
    {
        var pipeline = new OutputPipeline();
        pipeline.Process("Baixando 10%"u8);

        var result = pipeline.Process("\rBaixando 45%"u8);

        Assert.Equal("", result.Speech);
        Assert.Equal(45, result.ProgressPercent);
        Assert.Equal("Baixando 45%", result.Partial);
    }

    [Fact]
    public void BarraDeProgresso_GuardaSoAVersaoFinal()
    {
        var pipeline = new OutputPipeline();

        Assert.Equal("100%\r\n", pipeline.Process("10%\r50%\r100%\n"u8).Committed);
    }

    [Fact]
    public void PromptDeSenha_EDetectadoENaoEFalado()
    {
        var pipeline = new OutputPipeline();

        var result = pipeline.Process("[sudo] password for paulo: "u8);

        Assert.True(result.PasswordPrompt);
        Assert.Equal("", result.Speech);
    }

    [Fact]
    public void ComandoOculto_EnviadoNoPrimeiroPromptESemEco()
    {
        var pipeline = new OutputPipeline();
        pipeline.ScheduleHiddenCommand(" export PAGER=cat");

        var first = pipeline.Process("Bem-vindo\r\n$ "u8);
        Assert.Equal(" export PAGER=cat", first.CommandToSend);
        Assert.Equal("Bem-vindo\r\n", first.Speech);

        var echo = pipeline.Process(" export PAGER=cat\r\n$ "u8);
        Assert.Equal("", echo.Committed);
        Assert.Equal("$ ", echo.Speech);
        Assert.Null(echo.CommandToSend);
    }

    [Fact]
    public void TelaAlternativa_EInformada()
    {
        var pipeline = new OutputPipeline();

        Assert.True(pipeline.Process("\u001b[?1049h"u8).AlternateScreenEntered);
    }
}
