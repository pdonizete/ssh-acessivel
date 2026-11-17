using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AcessivelSsh.Core.Speech;

/// <summary>
/// Fala e braille pelo NVDA, usando a DLL nvdaControllerClient (extras/controllerClient do NVDA).
/// </summary>
/// <remarks>
/// P/Invoke é o jeito do C# chamar funções de uma DLL nativa (C/C++). Equivale ao
/// ctypes do Python: ctypes.WinDLL("x.dll").nvdaController_speakText("oi").
/// Aqui declaramos a assinatura de cada função com [LibraryImport], e o compilador
/// gera o código que converte a string do C# para wchar_t* (UTF-16).
///
/// "partial" permite que parte da classe seja escrita pelo gerador de código do
/// [LibraryImport]. Nós escrevemos só a declaração; o corpo é gerado no build.
///
/// Regra principal: nenhuma exceção da DLL sai desta classe. Se a DLL não existir,
/// for de arquitetura errada ou o NVDA estiver fechado, os métodos retornam false.
/// </remarks>
public sealed partial class NvdaScreenReader : IScreenReader
{
    // Nome lógico usado nos [LibraryImport]. O arquivo real é escolhido em EnsureLibraryLoaded.
    private const string LibraryName = "nvdaControllerClient";

    // O zip oficial do NVDA traz x64/nvdaControllerClient.dll; versões antigas usavam o sufixo 64.
    private static readonly string[] CandidateFileNames =
    [
        "nvdaControllerClient64.dll",
        "nvdaControllerClient.dll",
    ];

    // Campos static: a DLL é carregada uma vez por processo, não uma vez por objeto.
    private static readonly object s_loadLock = new();
    private static bool s_loadAttempted;
    private static IntPtr s_libraryHandle;
    private static string? s_libraryPath;

    public ScreenReaderStatus Status { get; private set; } = ScreenReaderStatus.LibraryNotFound;

    /// <summary>Caminho da DLL carregada, ou null se nenhuma foi encontrada.</summary>
    public string? LoadedLibraryPath => s_libraryPath;

    /// <summary>A DLL exporta nvdaController_speakSsml (NVDA 2024.1+). Só informativo: não dependemos dela.</summary>
    public bool SupportsSpeakSsml { get; private set; }

    /// <summary>A DLL exporta nvdaController_getProcessId (NVDA 2024.1+). Só informativo.</summary>
    public bool SupportsGetProcessId { get; private set; }

    /// <summary>Última exceção capturada numa chamada à DLL, para diagnóstico e log.</summary>
    public Exception? LastError { get; private set; }

    public ScreenReaderStatus Initialize()
    {
        EnsureLibraryLoaded();
        if (s_libraryHandle == IntPtr.Zero)
        {
            return Status = ScreenReaderStatus.LibraryNotFound;
        }

        // TryGetExport só verifica se o nome existe na DLL, sem chamar a função.
        SupportsSpeakSsml = NativeLibrary.TryGetExport(s_libraryHandle, "nvdaController_speakSsml", out _);
        SupportsGetProcessId = NativeLibrary.TryGetExport(s_libraryHandle, "nvdaController_getProcessId", out _);

        // testIfRunning retorna 0 quando o NVDA está rodando.
        return Status = CallNative(TestIfRunning) ? ScreenReaderStatus.Ready : ScreenReaderStatus.NotRunning;
    }

    public bool Speak(string text, bool interrupt = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }
        if (interrupt)
        {
            CallNative(CancelSpeech);
        }
        // "() => SpeakText(text)" é um lambda, como lambda: speak_text(text) no Python.
        return CallNative(() => SpeakText(text));
    }

    public bool Braille(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }
        return CallNative(() => BrailleMessage(text));
    }

    public bool Cancel() => CallNative(CancelSpeech);

    /// <summary>
    /// Executa uma função da DLL. Retorna true só se ela devolveu 0 (sucesso).
    /// Captura as exceções que a camada nativa pode gerar, para nunca derrubar o app.
    /// </summary>
    private bool CallNative(Func<int> nativeCall)
    {
        if (s_libraryHandle == IntPtr.Zero)
        {
            return false;
        }
        try
        {
            return nativeCall() == 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException
                                      or EntryPointNotFoundException
                                      or BadImageFormatException
                                      or SEHException
                                      or MarshalDirectiveException)
        {
            LastError = ex;
            return false;
        }
    }

    private static void EnsureLibraryLoaded()
    {
        // lock: como threading.Lock() do Python, usado com "with". Evita duas threads carregando ao mesmo tempo.
        lock (s_loadLock)
        {
            if (s_loadAttempted)
            {
                return;
            }
            s_loadAttempted = true;

            // Procuramos só ao lado do executável, com caminho absoluto. Assim não carregamos
            // por engano uma DLL com o mesmo nome que esteja em outra pasta do PATH.
            foreach (var fileName in CandidateFileNames)
            {
                var path = Path.Combine(AppContext.BaseDirectory, fileName);
                // TryLoad também falha (sem exceção) se a DLL for de 32 bits num processo x64.
                if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
                {
                    s_libraryHandle = handle;
                    s_libraryPath = path;
                    break;
                }
            }

            try
            {
                // Faz os [LibraryImport] com LibraryName usarem a DLL que acabamos de carregar.
                NativeLibrary.SetDllImportResolver(typeof(NvdaScreenReader).Assembly, ResolveLibrary);
            }
            catch (InvalidOperationException)
            {
                // O .NET só permite registrar um resolvedor por assembly. Se já houver um, seguimos.
            }
        }
    }

    private static IntPtr ResolveLibrary(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        // IntPtr.Zero = "não sei resolver", e o .NET usa a busca padrão.
        return libraryName == LibraryName ? s_libraryHandle : IntPtr.Zero;
    }

    // Declarações conforme nvdaController.idl: __stdcall, strings const wchar_t* (UTF-16),
    // retorno error_status_t (0 = sucesso, outro valor = código de erro do Windows).
    // No x64 só existe uma convenção de chamada, mas declaramos Stdcall para ficar igual ao header.

    [LibraryImport(LibraryName, EntryPoint = "nvdaController_testIfRunning")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial int TestIfRunning();

    [LibraryImport(LibraryName, EntryPoint = "nvdaController_speakText", StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial int SpeakText(string text);

    [LibraryImport(LibraryName, EntryPoint = "nvdaController_cancelSpeech")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial int CancelSpeech();

    [LibraryImport(LibraryName, EntryPoint = "nvdaController_brailleMessage", StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial int BrailleMessage(string message);
}
