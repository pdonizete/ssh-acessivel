using System.Security.Cryptography;
using SshAcessivel.Core.Persistence;

namespace SshAcessivel.Tests.Persistence;

/// <summary>Pasta temporária apagada no fim do teste, como tempfile.TemporaryDirectory() no Python.</summary>
/// <remarks>IDisposable + "using var" equivalem a um bloco "with".</remarks>
internal sealed class TempDir : IDisposable
{
    public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssh-acessivel-testes-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Root);

    public string FilePath(string name) => System.IO.Path.Combine(Root, name);

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { }
    }
}

public class SecretProtectorTests
{
    [Fact]
    public void ProtegerEDecifrar_VoltaOMesmoTexto()
    {
        var protegido = SecretProtector.Protect("s3nh@ com ação");

        Assert.DoesNotContain("s3nh@", protegido);
        Assert.True(SecretProtector.TryUnprotect(protegido, out var segredo));
        Assert.Equal("s3nh@ com ação", segredo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("isto não é base64!")]
    [InlineData("AAAAAAAA")]
    public void ValorInvalido_RetornaFalse(string? valor)
    {
        Assert.False(SecretProtector.TryUnprotect(valor, out var segredo));
        Assert.Equal("", segredo);
    }

    [Fact]
    public void ValorAlterado_RetornaFalse()
    {
        var bytes = Convert.FromBase64String(SecretProtector.Protect("segredo"));
        bytes[^1] ^= 0xFF;

        Assert.False(SecretProtector.TryUnprotect(Convert.ToBase64String(bytes), out _));
    }
}

public class ConnectionStoreTests
{
    private static ConnectionProfile Exemplo() => new()
    {
        Name = "Servidor de casa",
        Host = "192.168.0.10",
        Port = 2222,
        Username = "paulo",
        Authentication = AuthenticationKind.PrivateKey,
        PrivateKeyPath = @"C:\chaves\id_ed25519",
        Group = "Casa",
        ProtectedSecret = SecretProtector.Protect("minhaPassphrase"),
    };

    [Fact]
    public void SalvarECarregar_MantemOsCampos()
    {
        using var dir = new TempDir();
        var path = dir.FilePath("connections.json");
        var original = Exemplo();
        var store = new ConnectionStore(path);
        store.AddOrUpdate(original);
        store.Save();

        var outro = new ConnectionStore(path);
        outro.Load();

        var lido = Assert.Single(outro.Profiles);
        Assert.Equal(original.Id, lido.Id);
        Assert.Equal("Servidor de casa", lido.Name);
        Assert.Equal(2222, lido.Port);
        Assert.Equal(AuthenticationKind.PrivateKey, lido.Authentication);
        Assert.Equal("Casa", lido.Group);
        Assert.True(SecretProtector.TryUnprotect(lido.ProtectedSecret, out var segredo));
        Assert.Equal("minhaPassphrase", segredo);
    }

    [Fact]
    public void Arquivo_NaoTemSegredoEmTextoPuro()
    {
        using var dir = new TempDir();
        var path = dir.FilePath("connections.json");
        var store = new ConnectionStore(path);
        store.AddOrUpdate(Exemplo());
        store.Save();

        var texto = File.ReadAllText(path);
        Assert.DoesNotContain("minhaPassphrase", texto);
        Assert.Contains("\"PrivateKey\"", texto); // enum gravado como texto
    }

    [Fact]
    public void Exportar_NaoLevaSegredos()
    {
        using var dir = new TempDir();
        var store = new ConnectionStore(dir.FilePath("connections.json"));
        var perfil = Exemplo();
        store.AddOrUpdate(perfil);
        var exportado = dir.FilePath("exportado.json");

        store.Export(exportado);

        var texto = File.ReadAllText(exportado);
        Assert.Contains("192.168.0.10", texto);
        Assert.DoesNotContain(perfil.ProtectedSecret!, texto);
        Assert.True(store.Profiles[0].HasSavedSecret); // o original continua com o segredo
    }

    [Fact]
    public void Importar_IgnoraSegredosERepetidos()
    {
        using var dir = new TempDir();
        var arquivo = dir.FilePath("importar.json");
        File.WriteAllText(arquivo, """
            { "Connections": [
              { "Name": "A", "Host": "a.exemplo", "Port": 22, "Username": "u", "ProtectedSecret": "AAAA" },
              { "Name": "", "Host": "", "Port": 22, "Username": "u" },
              null
            ] }
            """);
        var store = new ConnectionStore(dir.FilePath("connections.json"));

        Assert.Equal(1, store.Import(arquivo));
        Assert.Equal(0, store.Import(arquivo));
        var perfil = Assert.Single(store.Profiles);
        Assert.Null(perfil.ProtectedSecret);
    }

    [Fact]
    public void ArquivoCorrompido_GuardaCopiaEComecaVazio()
    {
        using var dir = new TempDir();
        var path = dir.FilePath("connections.json");
        File.WriteAllText(path, "{ isso não é json");
        var store = new ConnectionStore(path);

        store.Load();

        Assert.True(store.LoadFailed);
        Assert.Empty(store.Profiles);
        Assert.True(File.Exists(store.BackupPath));
        store.AddOrUpdate(Exemplo());
        store.Save(); // com a cópia guardada, pode salvar por cima
    }

    [Fact]
    public void ArquivoInexistente_ListaVaziaSemErro()
    {
        using var dir = new TempDir();
        var store = new ConnectionStore(dir.FilePath("nao-existe.json"));

        store.Load();

        Assert.False(store.LoadFailed);
        Assert.Empty(store.Profiles);
    }
}

public class KnownHostsStoreTests
{
    [Fact]
    public void PrimeiraConexao_Desconhecida()
    {
        using var dir = new TempDir();
        var hosts = new KnownHostsStore(dir.FilePath("known_hosts"));

        Assert.Equal(HostKeyCheck.Unknown, hosts.Check("servidor", 22, "SHA256:abc"));
    }

    [Fact]
    public void ChaveSalva_EConfiavel_HostSemDiferencaDeMaiusculas()
    {
        using var dir = new TempDir();
        var hosts = new KnownHostsStore(dir.FilePath("known_hosts"));
        hosts.Add("Servidor", 22, "ssh-ed25519", "SHA256:abc");

        Assert.Equal(HostKeyCheck.Trusted, hosts.Check("servidor", 22, "SHA256:abc"));
    }

    [Fact]
    public void ChaveDiferente_EBloqueada()
    {
        using var dir = new TempDir();
        var hosts = new KnownHostsStore(dir.FilePath("known_hosts"));
        hosts.Add("servidor", 22, "ssh-ed25519", "SHA256:abc");

        Assert.Equal(HostKeyCheck.Mismatch, hosts.Check("servidor", 22, "SHA256:xyz"));
    }

    [Fact]
    public void OutraPorta_EDesconhecida()
    {
        using var dir = new TempDir();
        var hosts = new KnownHostsStore(dir.FilePath("known_hosts"));
        hosts.Add("servidor", 22, "ssh-ed25519", "SHA256:abc");

        Assert.Equal(HostKeyCheck.Unknown, hosts.Check("servidor", 2222, "SHA256:abc"));
    }
}

public class AppSettingsTests
{
    [Fact]
    public void ArquivoInvalido_UsaPadroes()
    {
        using var dir = new TempDir();
        var path = dir.FilePath("settings.json");
        File.WriteAllText(path, "lixo");

        var settings = AppSettings.Load(path);

        Assert.Equal(15, settings.ConnectTimeoutSeconds);
        Assert.Equal(30, settings.KeepAliveSeconds);
    }

    [Fact]
    public void ValoresForaDoLimite_SaoAjustados()
    {
        using var dir = new TempDir();
        var path = dir.FilePath("settings.json");
        File.WriteAllText(path, """{ "ConnectTimeoutSeconds": 0, "KeepAliveSeconds": 99999 }""");

        var settings = AppSettings.Load(path);

        Assert.Equal(3, settings.ConnectTimeoutSeconds);
        Assert.Equal(3600, settings.KeepAliveSeconds);
    }
}
