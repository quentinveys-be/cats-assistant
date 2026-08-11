using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace CatsAssistant.Store;

/// <summary>
/// Génère et protège la clé de chiffrement de la base activité avec DPAPI (portée CurrentUser).
/// La clé vit hors de la base, dans un fichier dédié, et n'exige aucun secret physique (décision #10) :
/// contrairement à la base métier (step-2.5), elle doit rester lisible sans YubiKey.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiActivityKeyStore
{
    private const int KeySizeInBytes = 32;

    private readonly string _keyFilePath;

    public DpapiActivityKeyStore(string keyFilePath)
    {
        _keyFilePath = keyFilePath;
    }

    public static string GetDefaultKeyFilePath()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatsAssistant");
        return Path.Combine(dataDirectory, "activity.key");
    }

    public string GetOrCreateKey()
    {
        if (File.Exists(_keyFilePath))
        {
            var protectedBytes = File.ReadAllBytes(_keyFilePath);
            var rawKey = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(rawKey);
        }

        var directory = Path.GetDirectoryName(_keyFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var newKey = RandomNumberGenerator.GetBytes(KeySizeInBytes);
        var protectedKey = ProtectedData.Protect(newKey, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_keyFilePath, protectedKey);
        return Convert.ToBase64String(newKey);
    }
}
