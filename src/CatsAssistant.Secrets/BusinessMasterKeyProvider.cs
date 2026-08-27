using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace CatsAssistant.Secrets;

/// <summary>
/// Dérive la clé maître de `business.db` (docs/adr/D6, step 2.5) par challenge-response HMAC-SHA1 YubiKey.
/// Le slot est enrôlé avec --touch : chaque dérivation exige un appui physique. La clé est donc dérivée une
/// seule fois puis gardée en mémoire pour tout le process — jamais redérivée à chaque synchronisation.
/// Le challenge (aléatoire, non sensible seul) est persisté pour que la clé dérivée reste stable entre les
/// démarrages ; contrairement à <see cref="DpapiYubiKeySecretVault"/>, aucun DPAPI ici : la base métier doit
/// rester inaccessible sans la YubiKey physique, y compris sur la même session Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class BusinessMasterKeyProvider
{
    private const int ChallengeSizeInBytes = 64;
    private const int DerivedKeySizeInBytes = 32;

    private readonly string _challengeFilePath;
    private readonly IYubiKeyChallengeResponseClient _yubiKeyClient;
    private string? _cachedKey;

    public BusinessMasterKeyProvider(string challengeFilePath, IYubiKeyChallengeResponseClient yubiKeyClient)
    {
        _challengeFilePath = challengeFilePath;
        _yubiKeyClient = yubiKeyClient;
    }

    public static string GetDefaultChallengeFilePath()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatsAssistant");
        return Path.Combine(dataDirectory, "business.challenge");
    }

    /// <summary>Vérification matérielle sans toucher la clé (pas de dérivation) : à utiliser par l'UI pour
    /// décider d'afficher le dialogue « Touchez votre YubiKey » ou de dégrader silencieusement.</summary>
    public bool IsYubiKeyPresent => _yubiKeyClient.IsPresent();

    /// <summary>
    /// Retourne la clé maître (Base64), ou null si la YubiKey est absente ou refuse la dérivation :
    /// l'appelant doit alors ouvrir l'app en mode dégradé sans accès métier (docs/adr/D6).
    /// </summary>
    public string? TryGetOrDeriveKey()
    {
        if (_cachedKey is not null)
        {
            return _cachedKey;
        }

        if (!_yubiKeyClient.IsPresent())
        {
            return null;
        }

        // Toute panne ici (challenge illisible/corrompu, YubiKey retirée pendant l'appel ou touch refusé/expiré
        // — le SDK Yubico lève InvalidOperationException sur un GetDataBytes() en échec, challenge rejeté) doit
        // dégrader plutôt que de faire échouer OnStartup : jamais de crash.
        try
        {
            var response = _yubiKeyClient.CalculateHmacSha1Response(GetOrCreateChallenge());
            try
            {
                var derivedKey = HKDF.DeriveKey(
                    HashAlgorithmName.SHA256,
                    response,
                    DerivedKeySizeInBytes,
                    salt: null,
                    info: Encoding.UTF8.GetBytes("CatsAssistant.Store.business.db"));
                try
                {
                    _cachedKey = Convert.ToBase64String(derivedKey);
                    return _cachedKey;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(derivedKey);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(response);
            }
        }
        catch (Exception ex) when (ex is YubiKeyNotPresentException or IOException or UnauthorizedAccessException
            or ArgumentException or CryptographicException or InvalidOperationException)
        {
            return null;
        }
    }

    private byte[] GetOrCreateChallenge()
    {
        if (File.Exists(_challengeFilePath))
        {
            var challenge = File.ReadAllBytes(_challengeFilePath);
            if (challenge.Length != ChallengeSizeInBytes)
            {
                // Écriture précédente interrompue (crash, coupure) : un challenge tronqué dériverait
                // silencieusement une mauvaise clé plutôt que de dégrader proprement.
                throw new IOException(
                    $"Challenge business.db corrompu ({challenge.Length} octets, {ChallengeSizeInBytes} attendus).");
            }

            return challenge;
        }

        var directory = Path.GetDirectoryName(_challengeFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var newChallenge = RandomNumberGenerator.GetBytes(ChallengeSizeInBytes);

        // Création atomique (fichier temporaire puis renommage sans overwrite) : jamais de challenge
        // partiellement écrit visible par une lecture concurrente ou un crash en plein milieu de l'écriture.
        // Sans overwrite, le renommage échoue si une autre instance a gagné la course entre-temps ; on relit
        // alors son challenge au lieu d'écraser le sien, sous peine de rendre business.db illisible.
        var tempPath = $"{_challengeFilePath}.tmp-{Guid.NewGuid():N}";
        File.WriteAllBytes(tempPath, newChallenge);
        try
        {
            File.Move(tempPath, _challengeFilePath);
            return newChallenge;
        }
        catch (IOException)
        {
            File.Delete(tempPath);
            var winningChallenge = File.ReadAllBytes(_challengeFilePath);
            if (winningChallenge.Length != ChallengeSizeInBytes)
            {
                throw new IOException(
                    $"Challenge business.db corrompu ({winningChallenge.Length} octets, {ChallengeSizeInBytes} attendus).");
            }

            return winningChallenge;
        }
    }
}
