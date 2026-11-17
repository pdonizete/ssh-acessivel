using AcessivelSsh.Core.Output;

namespace AcessivelSsh.Tests.Output;

public class AnsiStripperTests
{
    [Fact]
    public void RemoveCores()
    {
        Assert.Equal("ok", AnsiStripper.Strip("\u001b[1;32mok\u001b[0m").Text);
    }

    [Fact]
    public void RemoveTituloDaJanela()
    {
        Assert.Equal("texto", AnsiStripper.Strip("\u001b]0;usuario@host: ~\u0007texto").Text);
    }

    [Fact]
    public void RemoveMovimentoDeCursor()
    {
        Assert.Equal("ab", AnsiStripper.Strip("a\u001b[2K\u001b[10;5Hb").Text);
    }

    [Fact]
    public void SequenciaIncompleta_FicaPendente()
    {
        var result = AnsiStripper.Strip("abc\u001b[3");

        Assert.Equal("abc", result.Text);
        Assert.Equal("\u001b[3", result.Pending);
    }

    [Fact]
    public void TelaAlternativa_EDetectada()
    {
        Assert.True(AnsiStripper.Strip("\u001b[?1049h").AlternateScreenEntered);
        Assert.True(AnsiStripper.Strip("\u001b[?1049l").AlternateScreenExited);
        Assert.False(AnsiStripper.Strip("\u001b[?25h").AlternateScreenEntered);
    }

    [Fact]
    public void TextoSemEscape_VoltaIgual()
    {
        Assert.Equal("ação", AnsiStripper.Strip("ação").Text);
    }
}

public class LineAssemblerTests
{
    [Fact]
    public void CarriageReturnSozinho_ReescreveALinha()
    {
        var lines = new LineAssembler();

        var result = lines.Process("10%\r50%\r100%\n");

        var line = Assert.Single(result.CompletedLines);
        Assert.Equal("100%", line.Text);
        Assert.True(line.WasOverwritten);
    }

    [Fact]
    public void CrLf_EQuebraNormal()
    {
        var lines = new LineAssembler();

        var result = lines.Process("a\r\nb");

        var line = Assert.Single(result.CompletedLines);
        Assert.Equal("a", line.Text);
        Assert.False(line.WasOverwritten);
        Assert.Equal("b", lines.Partial);
    }

    [Fact]
    public void Backspace_SubstituiCaractere()
    {
        var lines = new LineAssembler();

        Assert.Equal("abd", Assert.Single(lines.Process("abc\bd\n").CompletedLines).Text);
    }
}

public class DetectorsTests
{
    [Theory]
    [InlineData("[sudo] password for paulo: ", true)]
    [InlineData("Password:", true)]
    [InlineData("Senha: ", true)]
    [InlineData("[sudo] senha para paulo: ", true)]
    [InlineData("Enter passphrase for key '/home/paulo/.ssh/id_ed25519': ", true)]
    [InlineData("A senha foi alterada com sucesso.", false)]
    [InlineData("paulo@host:~$ ", false)]
    [InlineData("", false)]
    public void PasswordPrompt(string line, bool expected)
    {
        Assert.Equal(expected, PasswordPromptDetector.IsPasswordPrompt(line));
    }

    [Theory]
    [InlineData("sudo vim /etc/hosts", "vim")]
    [InlineData("git log | less", "less")]
    [InlineData("/usr/bin/nano arquivo", "nano")]
    [InlineData("htop", "htop")]
    [InlineData("ls -la", null)]
    [InlineData("echo vim", null)]
    public void FullScreen(string command, string? expected)
    {
        Assert.Equal(expected, FullScreenDetector.DetectProgram(command));
    }
}
