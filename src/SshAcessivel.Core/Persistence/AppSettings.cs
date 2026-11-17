using System.Text.Json;

namespace SshAcessivel.Core.Persistence;

/// <summary>Configurações do app (settings.json), editadas em Ferramentas > Configurações.</summary>
public sealed class AppSettings
{
    /// <summary>Tempo máximo para conectar e autenticar, em segundos (3 a 300).</summary>
    public int ConnectTimeoutSeconds { get; set; } = 15;

    /// <summary>Intervalo do keep-alive em segundos (0 desliga, máximo 3600).</summary>
    public int KeepAliveSeconds { get; set; } = 30;

    /// <summary>Silêncio antes de falar a saída acumulada, em milissegundos (50 a 2000).</summary>
    public int SpeechDebounceMilliseconds { get; set; } = 150;

    /// <summary>Acima deste número de linhas, fala só "N linhas recebidas" (5 a 10000).</summary>
    public int SummaryLineThreshold { get; set; } = 30;

    /// <summary>Intervalo mínimo entre falas de porcentagem de progresso, em segundos (1 a 120).</summary>
    public int ProgressIntervalSeconds { get; set; } = 5;

    /// <summary>Linhas mantidas no campo de saída (500 a 100000).</summary>
    public int MaxOutputLines { get; set; } = 5000;

    /// <summary>Comandos guardados no histórico de cada servidor (10 a 10000).</summary>
    public int HistorySize { get; set; } = 500;

    /// <summary>Tab no campo de comando envia Tab ao servidor (autocompletar). Desligado, Tab move o foco.</summary>
    public bool TabSendsToServer { get; set; }

    /// <summary>Atalhos: nome da ação -> teclas (ex.: "Ctrl+Shift+A"). Ações ausentes usam o padrão.</summary>
    public Dictionary<string, string> Shortcuts { get; set; } = new();

    /// <summary>Lê o arquivo. Se ele não existir ou estiver inválido, usa os valores padrão.</summary>
    public static AppSettings Load(string path)
    {
        AppSettings? settings = null;
        try
        {
            if (File.Exists(path))
            {
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), ConnectionStore.JsonOptions);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Arquivo inválido: segue com os padrões.
        }
        settings ??= new AppSettings();
        settings.ConnectTimeoutSeconds = Math.Clamp(settings.ConnectTimeoutSeconds, 3, 300);
        settings.KeepAliveSeconds = Math.Clamp(settings.KeepAliveSeconds, 0, 3600);
        settings.SpeechDebounceMilliseconds = Math.Clamp(settings.SpeechDebounceMilliseconds, 50, 2000);
        settings.SummaryLineThreshold = Math.Clamp(settings.SummaryLineThreshold, 5, 10000);
        settings.ProgressIntervalSeconds = Math.Clamp(settings.ProgressIntervalSeconds, 1, 120);
        settings.MaxOutputLines = Math.Clamp(settings.MaxOutputLines, 500, 100000);
        settings.HistorySize = Math.Clamp(settings.HistorySize, 10, 10000);
        settings.Shortcuts ??= new();
        return settings;
    }

    public void Save(string path) => AtomicFile.WriteAllText(path, JsonSerializer.Serialize(this, ConnectionStore.JsonOptions));
}
