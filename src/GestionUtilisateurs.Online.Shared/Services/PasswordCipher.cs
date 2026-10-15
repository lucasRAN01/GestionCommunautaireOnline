using System.Security.Cryptography;
using System.Text;

namespace GestionUtilisateurs.Shared.Services;

/// <summary>
/// Chiffrement réversible des mots de passe stockés localement.
/// Deux formats coexistent (migration transparente) :
/// - « enc: »  : historique AES-GCM. Lecture seule : AesGcm n'existe pas en
///   WebAssembly (Cryptography_AlgorithmNotSupported même en contexte sécurisé),
///   mais reste lisible sur plateforme native (MAUI). Ne pas utiliser pour chiffrer.
/// - « enc2: » : flux SHA256-CTR + HMAC-SHA256 (Encrypt-then-MAC), construit
///   uniquement avec SHA-256 / HMAC / RNG, 100 % managés donc fonctionnels
///   partout y compris Blazor WebAssembly sur http. Utilisé par Encrypt()
///   sur toutes les plateformes (nouveaux mots de passe).
/// Note : la clé maîtresse est embarquée dans l'application (comme avant) :
/// il s'agit d'obscurcissement anti-lecture-casuelle du stockage local,
/// pas d'une protection contre un attaquant accédant au binaire.
/// Les anciennes valeurs en clair (sans préfixe) restent lisibles.
/// </summary>
public static class PasswordCipher
{
    private const string Prefix = "enc:";
    private const string Prefix2 = "enc2:";
    private const byte PayloadVersion = 0x02;

    private static readonly byte[] MasterKey = SHA256.HashData(
        Encoding.UTF8.GetBytes("GestionUtilisateurs::2026::hors-ligne::v1"));

    private static byte[] SubKey(byte domain, byte[] salt)
    {
        var buf = new byte[MasterKey.Length + 1 + salt.Length];
        Buffer.BlockCopy(MasterKey, 0, buf, 0, MasterKey.Length);
        buf[MasterKey.Length] = domain;
        Buffer.BlockCopy(salt, 0, buf, MasterKey.Length + 1, salt.Length);
        return SHA256.HashData(buf);
    }

    /// <summary>Flux pseudo-aléatoire SHA256-CTR : SHA256(ke || salt || compteur BE).</summary>
    private static void XorKeyStream(byte[] ke, byte[] salt, Span<byte> data)
    {
        var ctr = new byte[32 + salt.Length + 4];
        Buffer.BlockCopy(ke, 0, ctr, 0, 32);
        Buffer.BlockCopy(salt, 0, ctr, 32, salt.Length);
        var offset = 0;
        uint counter = 0;
        while (offset < data.Length)
        {
            ctr[ctr.Length - 4] = (byte)(counter >> 24);
            ctr[ctr.Length - 3] = (byte)(counter >> 16);
            ctr[ctr.Length - 2] = (byte)(counter >> 8);
            ctr[ctr.Length - 1] = (byte)counter;
            var block = SHA256.HashData(ctr);
            var n = Math.Min(block.Length, data.Length - offset);
            for (var i = 0; i < n; i++) data[offset + i] ^= block[i];
            offset += n;
            counter++;
        }
        CryptographicOperations.ZeroMemory(ctr);
    }

    public static bool IsEncrypted(string stored) =>
        stored.StartsWith(Prefix2, StringComparison.Ordinal) ||
        stored.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Chiffre en SHA256-CTR + HMAC (lisible partout, Web inclus).</summary>
    public static string Encrypt(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        if (IsEncrypted(plain)) return plain;

        var plainBytes = Encoding.UTF8.GetBytes(plain);
        var salt = RandomNumberGenerator.GetBytes(16);
        var ke = SubKey(0x01, salt);
        var km = SubKey(0x02, salt);
        try
        {
            var cipher = (byte[])plainBytes.Clone();
            XorKeyStream(ke, salt, cipher);
            byte[] mac;
            using (var hmac = new HMACSHA256(km))
            {
                var saltAndCipher = new byte[salt.Length + cipher.Length];
                Buffer.BlockCopy(salt, 0, saltAndCipher, 0, salt.Length);
                Buffer.BlockCopy(cipher, 0, saltAndCipher, salt.Length, cipher.Length);
                mac = hmac.ComputeHash(saltAndCipher);
            }
            var all = new byte[1 + salt.Length + mac.Length + cipher.Length];
            all[0] = PayloadVersion;
            Buffer.BlockCopy(salt, 0, all, 1, salt.Length);
            Buffer.BlockCopy(mac, 0, all, 1 + salt.Length, mac.Length);
            Buffer.BlockCopy(cipher, 0, all, 1 + salt.Length + mac.Length, cipher.Length);
            return Prefix2 + Convert.ToBase64String(all);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ke);
            CryptographicOperations.ZeroMemory(km);
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public static string Decrypt(string stored)
    {
        if (string.IsNullOrEmpty(stored) || !IsEncrypted(stored)) return stored ?? "";
        if (stored.StartsWith(Prefix2, StringComparison.Ordinal))
            return DecryptCtr(stored);
        return DecryptGcm(stored);
    }

    private static string DecryptCtr(string stored)
    {
        try
        {
            var all = Convert.FromBase64String(stored[Prefix2.Length..]);
            if (all.Length < 49 || all[0] != PayloadVersion) return "";
            var salt = all.AsSpan(1, 16).ToArray();
            var mac = all.AsSpan(17, 32).ToArray();
            var cipher = all.AsSpan(49).ToArray();
            var km = SubKey(0x02, salt);
            try
            {
                byte[] expected;
                using (var hmac = new HMACSHA256(km))
                {
                    var saltAndCipher = new byte[salt.Length + cipher.Length];
                    Buffer.BlockCopy(salt, 0, saltAndCipher, 0, salt.Length);
                    Buffer.BlockCopy(cipher, 0, saltAndCipher, salt.Length, cipher.Length);
                    expected = hmac.ComputeHash(saltAndCipher);
                }
                if (!CryptographicOperations.FixedTimeEquals(mac, expected)) return "";
            }
            finally
            {
                CryptographicOperations.ZeroMemory(km);
            }
            var ke = SubKey(0x01, salt);
            try
            {
                XorKeyStream(ke, salt, cipher);
                return Encoding.UTF8.GetString(cipher);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(ke);
                CryptographicOperations.ZeroMemory(cipher);
            }
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Lecture seule de l'historique AES-GCM (échoue silencieusement
    /// là où AesGcm n'existe pas, ex. navigateur : Verify retourne alors false).</summary>
    private static string DecryptGcm(string stored)
    {
        try
        {
            var all = Convert.FromBase64String(stored[Prefix.Length..]);
            if (all.Length < 28) return "";
            var iv = all.AsSpan(0, 12).ToArray();
            var tag = all.AsSpan(12, 16).ToArray();
            var cipherBytes = all.AsSpan(28).ToArray();
            var plainBytes = new byte[cipherBytes.Length];
            using var aes = new AesGcm(MasterKey, 16);
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
    {
        if (stored.StartsWith(Prefix2, StringComparison.Ordinal)) return stored[Prefix2.Length..];
        return IsEncrypted(stored) ? stored[Prefix.Length..] : stored;
    }
}
