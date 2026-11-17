using Renci.SshNet;
using Renci.SshNet.Common;

namespace SshAcessivel.Core.Ssh;

public enum PrivateKeyCheck
{
    Ok,
    NotFound,
    Unreadable,
    PassphraseRequired,
    /// <summary>Passphrase errada, ou o arquivo não é uma chave num formato suportado.</summary>
    InvalidOrWrongPassphrase,
}

/// <summary>Testa se uma chave privada abre, antes de conectar. Lê disco: chamar fora da thread da UI.</summary>
public static class PrivateKeyLoader
{
    public static PrivateKeyCheck Check(string path, string? passphrase)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return PrivateKeyCheck.NotFound;
        }
        try
        {
            var key = string.IsNullOrEmpty(passphrase) ? new PrivateKeyFile(path) : new PrivateKeyFile(path, passphrase);
            (key as IDisposable)?.Dispose();
            return PrivateKeyCheck.Ok;
        }
        catch (SshPassPhraseNullOrEmptyException)
        {
            return PrivateKeyCheck.PassphraseRequired;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PrivateKeyCheck.Unreadable;
        }
        catch (Exception)
        {
            // O SSH.NET lança tipos variados para chave inválida ou passphrase errada.
            return PrivateKeyCheck.InvalidOrWrongPassphrase;
        }
    }
}
