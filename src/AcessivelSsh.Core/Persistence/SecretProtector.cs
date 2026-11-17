using System.Security.Cryptography;
using System.Text;

namespace AcessivelSsh.Core.Persistence;

/// <summary>
/// Cifra senhas e passphrases com o DPAPI do Windows (escopo do usuário).
/// Só o mesmo usuário do Windows, no mesmo computador, consegue decifrar.
/// </summary>
public static class SecretProtector
{
    // "Entropia" extra: outro programa do mesmo usuário precisa conhecer este valor para decifrar.
    private static readonly byte[] Entropy = "AcessivelSsh.v1"u8.ToArray();

    /// <summary>Retorna o segredo cifrado, em base64, para guardar no JSON.</summary>
    public static string Protect(string secret)
    {
        var plain = Encoding.UTF8.GetBytes(secret);
        try
        {
            return Convert.ToBase64String(ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser));
        }
        finally
        {
            // Apaga a cópia em bytes da memória assim que possível.
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>Decifra um segredo. Retorna false se não houver segredo ou se ele não puder ser decifrado.</summary>
    /// <remarks>
    /// "out" devolve um segundo resultado pelo parâmetro. Em Python seria retornar a tupla (ok, segredo).
    /// </remarks>
    public static bool TryUnprotect(string? protectedSecret, out string secret)
    {
        secret = "";
        if (string.IsNullOrEmpty(protectedSecret))
        {
            return false;
        }
        try
        {
            var plain = ProtectedData.Unprotect(Convert.FromBase64String(protectedSecret), Entropy, DataProtectionScope.CurrentUser);
            try
            {
                secret = Encoding.UTF8.GetString(plain);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
            }
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Outro usuário, outro computador ou arquivo alterado.
            return false;
        }
    }
}
