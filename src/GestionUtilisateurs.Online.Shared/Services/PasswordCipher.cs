using System.Security.Cryptography;
using System.Text;

namespace GestionUtilisateurs.Shared.Services;

/// <summary>
/// Chiffrement réversible (AES-GCM) des mots de passe stockés localement.
/// Les mots de passe cryptés portent le préfixe « enc: ». Les anciennes valeurs
/// en clair (sans préfixe) restent lisibles pour migration en douceur.
/// </summary>
public static class PasswordCipher
{
    private const string Prefix = "enc:";

    private static readonly byte[] Key = SHA256.HashData(
        Encoding.UTF8.GetBytes("GestionUtilisateurs::2026::hors-ligne::v1"));

    public static bool IsEncrypted(string stored) => stored.StartsWith(Prefix, System.StringComparison.Ordinal);

    public static string Encrypt(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        if (IsEncrypted(plain)) return plain;

        var plainBytes = Encoding.UTF8.GetBytes(plain);
        var iv = RandomNumberGenerator.GetBytes(12);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(Key, 16);
        aes.Encrypt(iv, plainBytes, cipherBytes, tag);

        var all = new byte[iv.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, all, 0, iv.Length);
        Buffer.BlockCopy(tag, 0, all, iv.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, all, iv.Length + tag.Length, cipherBytes.Length);

        return Prefix + Convert.ToBase64String(all);
    }

    public static string Decrypt(string stored)
    {
        if (string.IsNullOrEmpty(stored) || !IsEncrypted(stored)) return stored ?? "";
        try
        {
            var all = Convert.FromBase64String(stored[Prefix.Length..]);
            if (all.Length < 28) return "";
            var iv = all.AsSpan(0, 12).ToArray();
            var tag = all.AsSpan(12, 16).ToArray();
            var cipherBytes = all.AsSpan(28).ToArray();
            var plainBytes = new byte[cipherBytes.Length];
            using var aes = new AesGcm(Key, 16);
            aes.Decrypt(iv, cipherBytes, tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return "";
        }
    }

    public static bool Verify(string stored, string attempt)
    {
        if (string.IsNullOrEmpty(attempt)) return false;
        if (IsEncrypted(stored))
        {
            var expected = Decrypt(stored);
            if (string.IsNullOrEmpty(expected)) return false;
            var exp = Encoding.UTF8.GetBytes(expected);
            var att = Encoding.UTF8.GetBytes(attempt);
            return exp.Length == att.Length && CryptographicOperations.FixedTimeEquals(exp, att);
        }
        return stored == attempt;
    }

    /// <summary>Texte crypté sans le préfixe (affichage « mode debug »).</summary>
    public static string CipherText(string stored)
        => IsEncrypted(stored) ? stored[Prefix.Length..] : stored;
}