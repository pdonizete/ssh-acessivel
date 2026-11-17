using SshAcessivel.App.Resources;
using SshAcessivel.Core.Persistence;
using SshAcessivel.Core.Speech;
using SshAcessivel.Core.Ssh;

namespace SshAcessivel.App;

static class Program
{
    // [STAThread] é exigido pelo Windows Forms (modelo de threads COM da janela e da área de transferência).
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // O app se chamava AcessivelSsh. Na primeira abertura com o nome novo, copia os dados
        // (conexões, known_hosts, histórico, configurações) para %APPDATA%\ssh-acessivel\.
        // Precisa vir antes de qualquer log ou leitura de configuração.
        bool migrated = AppPaths.MigrateLegacyData();

        // Uma exceção não tratada num evento da interface mostra uma mensagem em vez de fechar o app.
        // Um app que fecha sem aviso deixa o usuário do NVDA sem saber o que aconteceu.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            AppLog.Error("Exceção não tratada na interface", e.Exception);
            MessageBox.Show(string.Format(Strings.Error_Unexpected, e.Exception.Message),
                            Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        // Erros fora da thread da interface: o app fecha, mas o motivo fica no log.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLog.Error("Exceção fatal", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("Exceção numa tarefa de fundo", e.Exception);
            e.SetObserved();
        };

        if (migrated)
        {
            AppLog.Info($"Dados copiados de {AppPaths.LegacyDataDirectory} para {AppPaths.DataDirectory}.");
        }

        var reader = new NvdaScreenReader();
        var status = reader.Initialize();
        if (status != ScreenReaderStatus.Ready)
        {
            AppLog.Info($"NVDA: {status}. {reader.LastError?.Message}");
        }

        // Aviso único na barra de status sobre o estado do NVDA.
        var initialStatus = status switch
        {
            ScreenReaderStatus.Ready => string.Format(Strings.Status_Ready, DescribeOptional(reader)),
            ScreenReaderStatus.NotRunning => Strings.Status_NvdaNotRunning,
            _ => Strings.Status_DllNotFound,
        };

        using var session = new SshSession();
        Application.Run(new MainForm(reader, session, initialStatus));
    }

    private static string DescribeOptional(NvdaScreenReader reader)
    {
        var names = new List<string>();
        if (reader.SupportsSpeakSsml) names.Add("speakSsml");
        if (reader.SupportsGetProcessId) names.Add("getProcessId");
        return names.Count == 0 ? Strings.Status_OptionalNone : string.Join(", ", names);
    }
}
