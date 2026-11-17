using SshAcessivel.Core.Speech;
using Microsoft.Extensions.Time.Testing;

namespace SshAcessivel.Tests.Speech;

// FakeTimeProvider é um relógio falso: Advance() avança o tempo e dispara os timers na hora,
// sem esperar de verdade. Os testes ficam rápidos e sempre dão o mesmo resultado.
public class SpeechQueueTests
{
    private static (SpeechQueue Queue, FakeScreenReader Reader, FakeTimeProvider Time) Create()
    {
        var reader = new FakeScreenReader();
        var time = new FakeTimeProvider();
        var queue = new SpeechQueue(reader, new SpeechQueueOptions(), time);
        return (queue, reader, time);
    }

    [Fact]
    public void Pedacos_Proximos_SaoFaladosJuntos()
    {
        var (queue, reader, time) = Create();

        queue.Enqueue("a");
        time.Advance(TimeSpan.FromMilliseconds(50));
        queue.Enqueue("b");
        time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Empty(reader.Spoken); // ainda não houve 150 ms de silêncio

        time.Advance(TimeSpan.FromMilliseconds(60));
        Assert.Equal(["ab"], reader.Spoken);
    }

    [Fact]
    public void SaidaContinua_FalaAoAtingirEsperaMaxima()
    {
        var (queue, reader, time) = Create();

        for (int i = 0; i < 10; i++)
        {
            queue.Enqueue("x");
            time.Advance(TimeSpan.FromMilliseconds(100));
        }

        Assert.Equal(["xxxxxxxxxx"], reader.Spoken);
    }

    [Fact]
    public void SaidaGrande_FalaResumo()
    {
        var (queue, reader, time) = Create();

        queue.Enqueue(string.Concat(Enumerable.Repeat("linha\r\n", 40)));
        time.Advance(TimeSpan.FromMilliseconds(200));

        Assert.Equal(["40 linhas recebidas"], reader.Spoken);
    }

    [Fact]
    public void LeituraAutomaticaDesligada_IgnoraSaidaMasFalaAvisos()
    {
        var (queue, reader, time) = Create();
        queue.AutoRead = false;

        queue.Enqueue("saída");
        queue.SpeakNow("Conectado");
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(["Conectado"], reader.Spoken);
    }

    [Fact]
    public void Mudo_NaoFalaNada()
    {
        var (queue, reader, time) = Create();
        queue.Muted = true;

        queue.Enqueue("saída");
        queue.SpeakNow("aviso");
        queue.ReportProgress(50);
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(reader.Spoken);
    }

    [Fact]
    public void SpeakNow_DescartaSaidaPendente()
    {
        var (queue, reader, time) = Create();

        queue.Enqueue("antiga");
        queue.SpeakNow("aviso");
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(["aviso"], reader.Spoken);
    }

    [Fact]
    public void Progresso_RespeitaIntervaloMinimo()
    {
        var (queue, reader, time) = Create();

        queue.ReportProgress(10);
        time.Advance(TimeSpan.FromSeconds(1));
        queue.ReportProgress(20);
        time.Advance(TimeSpan.FromSeconds(5));
        queue.ReportProgress(30);

        Assert.Equal(["10 por cento", "30 por cento"], reader.Spoken);
    }

    [Fact]
    public void Cancel_DescartaPendenteEInterrompe()
    {
        var (queue, reader, time) = Create();

        queue.Enqueue("texto");
        queue.Cancel();
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(reader.Spoken);
        Assert.Equal(1, reader.CancelCount);
    }
}
