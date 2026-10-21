using System.Security.Cryptography;
using System.Text;

namespace Aveniq.Diagnostics;

public enum SecretLoadResult { Loaded, Missing, Unreadable }

/// <summary>
/// Stores small secrets with Windows DPAPI (CurrentUser scope). The protected blob can only be decrypted by the same
/// Windows user on the same machine, so copying the file elsewhere (backups, cloud sync) does not leak the secret.
/// </summary>
public static class SecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Aveniq.v1.secret");

    // CloudPlayX was the product's previous name. Existing installations still have DPAPI blobs created with this
    // entropy, so loading must remain backward compatible. Successful legacy reads are migrated to Entropy above.
    private static readonly byte[] LegacyEntropy = Encoding.UTF8.GetBytes("CloudPlayX.v1.secret");

    private static readonly byte[][] Entropies = [Entropy, LegacyEntropy];

    public static void Save(string path, string secret)
    {
        var blob = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, blob);
        File.Move(tmp, path, overwrite: true);
    }

    public static SecretLoadResult TryLoad(string path, out string? secret)
    {
        secret = null;
        if (!File.Exists(path)) return SecretLoadResult.Missing;

        byte[] data;
        try { data = File.ReadAllBytes(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return SecretLoadResult.Unreadable; }

        foreach (var entropy in Entropies)
        {
            try
            {
                secret = Encoding.UTF8.GetString(ProtectedData.Unprotect(data, entropy, DataProtectionScope.CurrentUser));
                if (!ReferenceEquals(entropy, Entropy)) TryMigrate(path, secret);
                return SecretLoadResult.Loaded;
            }
            catch (CryptographicException) { }
        }
        return SecretLoadResult.Unreadable;
    }

    public static string? Load(string path) =>
        TryLoad(path, out var secret) == SecretLoadResult.Loaded ? secret : null;

    /// <summary>Best-effort upgrade of a legacy blob; a read-only profile must not make an otherwise valid key unusable.</summary>
    private static void TryMigrate(string path, string secret)
    {
        try { Save(path, secret); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException) { }
    }

    public static string Protect(string secret) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser));

    public static string? Unprotect(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        byte[] data;
        try { data = Convert.FromBase64String(base64); }
        catch (FormatException) { return null; }
        foreach (var entropy in Entropies)
        {
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, entropy, DataProtectionScope.CurrentUser)); }
            catch (CryptographicException) { }
        }
        return null;
    }

    /// <summary>Cryptographically random URL-safe token.</summary>
    public static string RandomToken(int bytes = 24) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
