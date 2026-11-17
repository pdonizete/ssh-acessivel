using AcessivelSsh.Core.Output;
using AcessivelSsh.Core.Persistence;
using AcessivelSsh.Core.Speech;
using AcessivelSsh.Tests.Speech;
using Microsoft.Extensions.Time.Testing;

namespace AcessivelSsh.Tests.Persistence;

public class CommandHistoryTests
{
    [Fact]
    public void NavegaPeloHistoricoEVoltaAoRascunho()
    {
        var history = new CommandHistory(10);
        history.Add("ls");
        history.Add("pwd");

        Assert.Equal("pwd", history.Previous("rascunho"));
        Assert.Equal("ls", history.Previous("pwd"));
        Assert.Equal("ls", history.Previous("ls")); // no primeiro item, fica nele
        Assert.Equal("pwd", history.Next());
        Assert.Equal("rascunho", history.Next());
        Assert.Null(history.Next());
    }

    [Fact]
    public void HistoricoVazio_RetornaNull()
    {
        Assert.Null(new CommandHistory(10).Previous(""));
    }

    [Fact]
    public void NaoRepeteComandoSeguidoNemGuardaVazio()
    {
        var history = new CommandHistory(10);
        history.Add("ls");
        history.Add("ls");
        history.Add("   ");

        Assert.Equal(new[] { "ls" }, history.Entries);
    }

    [Fact]
    public void LimiteDescartaOsMaisAntigos()
    {
        var history = new CommandHistory(3);
        foreach (var c in new[] { "a", "b", "c", "d" })
        {
            history.Add(c);
        }

        Assert.Equal(new[] { "b", "c", "d" }, history.Entries);
    }

    [Fact]
    public async Task SalvarECarregar_MantemAcentos()
    {
        using var dir = new TempDir();
        var path = dir.FilePath("historico.json");
        var history = new CommandHistory(10);
        history.Add("echo ação");
        history.Add("ls");

        await history.SaveAsync(path, "teste@host:22");
        var loaded = CommandHistory.Load(path, 10);

        Assert.Equal(new[] { "echo ação", "ls" }, loaded.Entries);
    }

    [Fact]
    public void ArquivoPorServidor_IgnoraMaiusculasDoHost()
    {
        var a = CommandHistory.FileFor("d", "Servidor", 22, "paulo");
        var b = CommandHistory.FileFor("d", "servidor", 22, "paulo");
        var c = CommandHistory.FileFor("d", "servidor", 22, "outro");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}

public class SessionLogTests
{
    [Fact]
    public void GravaOTextoEmUtf8()
    {
        using var dir = new TempDir();
        var path = dir.FilePath("sessao.txt");
        using (var log = new SessionLog(path))
        {
            log.Write("ação\r\n");
            log.WriteLine("fim");
        }

        Assert.Equal("ação\r\nfim\r\n", File.ReadAllText(path));
    }
}

public class Phase5PipelineTests
{
    [Fact]
    public void Resposta_TemAsLinhasSemOEco()
    {
        var pipeline = new OutputPipeline();
        pipeline.NotifyCommandSent("ls");

        var result = pipeline.Process("ls\r\na\r\nb\r\n$ "u8);

        Assert.Equal("a\r\nb\r\n", result.Response);
    }

    [Fact]
    public void FilaDeFala_NovaPausaValeNoProximoLote()
    {
        var reader = new FakeScreenReader();
        var time = new FakeTimeProvider();
        using var queue = new SpeechQueue(reader, new SpeechQueueOptions(), time);
        queue.Options = new SpeechQueueOptions { Debounce = TimeSpan.FromMilliseconds(500), MaxDelay = TimeSpan.FromSeconds(2) };

        queue.Enqueue("texto");
        time.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Empty(reader.Spoken);

        time.Advance(TimeSpan.FromMilliseconds(400));
        Assert.Equal(new[] { "texto" }, reader.Spoken);
    }
}
