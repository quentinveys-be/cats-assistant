using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace CatsAssistant.Secrets;

/// <summary>
/// Coffre à deux facteurs (ADR D6) : la clé de chiffrement de chaque secret est dérivée d'une réponse
/// challenge-response HMAC-SHA1 YubiKey (possession physique), puis le fichier résultant est protégé DPAPI
/// portée CurrentUser (couche complémentaire liée au compte Windows). Lire un secret exige donc les deux :
/// être sur la même session Windows ET avoir la même YubiKey physiquement présente.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiYubiKeySecretVault : ISecretVault
{
    private const int ChallengeSizeInBytes = 64;
    private const int NonceSizeInBytes = 12;
    private const int TagSizeInBytes = 16;
    private const int DerivedKeySizeInBytes = 32;

    private readonly string _vaultDirectory;
    private readonly IYubiKeyChallengeResponseClient _yubiKeyClient;

    public DpapiYubiKeySecretVault(string vaultDirectory, IYubiKeyChallengeResponseClient yubiKeyClient)
    {
        _vaultDirectory = vaultDirectory;
        _yubiKeyClient = yubiKeyClient;
    }

    public static string GetDefaultVaultDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatsAssistant",
            "secrets");
    }

    public bool IsYubiKeyPresent => _yubiKeyClient.IsPresent();

    public void Store(SecretName name, string secretValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(secretValue);

        var challenge = RandomNumberGenerator.GetBytes(ChallengeSizeInBytes);
        var derivedKey = DeriveKey(challenge, name);
        var plaintext = Encoding.UTF8.GetBytes(secretValue);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeInBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeInBytes];

        try
        {
            using var aesGcm = new AesGcm(derivedKey, TagSizeInBytes);
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }

        var payload = Concat(challenge, nonce, tag, ciphertext);
        var protectedPayload = ProtectedData.Protect(payload, optionalEntropy: null, DataProtectionScope.CurrentUser);

        Directory.CreateDirectory(_vaultDirectory);
        File.WriteAllBytes(GetFilePath(name), protectedPayload);
    }

    public string? TryRead(SecretName name)
    {
        var filePath = GetFilePath(name);
        if (!File.Exists(filePath))
        {
            return null;
        }

        var protectedPayload = File.ReadAllBytes(filePath);
        var payload = ProtectedData.Unprotect(protectedPayload, optionalEntropy: null, DataProtectionScope.CurrentUser);

        var challenge = payload[..ChallengeSizeInBytes];
        var nonce = payload[ChallengeSizeInBytes..(ChallengeSizeInBytes + NonceSizeInBytes)];
        var tag = payload[(ChallengeSizeInBytes + NonceSizeInBytes)..(ChallengeSizeInBytes + NonceSizeInBytes + TagSizeInBytes)];
        var ciphertext = payload[(ChallengeSizeInBytes + NonceSizeInBytes + TagSizeInBytes)..];

        var derivedKey = DeriveKey(challenge, name);
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aesGcm = new AesGcm(derivedKey, TagSizeInBytes);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException ex)
        {
            throw new SecretVaultException(
                $"Le secret « {name} » n'a pas pu être déchiffré (YubiKey différente ou coffre corrompu).", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public void Delete(SecretName name)
    {
        var filePath = GetFilePath(name);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private byte[] DeriveKey(byte[] challenge, SecretName name)
    {
        if (!_yubiKeyClient.IsPresent())
        {
            throw new YubiKeyNotPresentException();
        }

        var response = _yubiKeyClient.CalculateHmacSha1Response(challenge);
        try
        {
            return HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                response,
                DerivedKeySizeInBytes,
                salt: null,
                info: Encoding.UTF8.GetBytes($"CatsAssistant.Secrets.{name}"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(response);
        }
    }

    private string GetFilePath(SecretName name) => Path.Combine(_vaultDirectory, $"{name}.secret");

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }
}
