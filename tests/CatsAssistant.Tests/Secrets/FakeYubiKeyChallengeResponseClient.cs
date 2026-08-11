using System.Security.Cryptography;
using CatsAssistant.Secrets;

namespace CatsAssistant.Tests.Secrets;

/// <summary>
/// Simule le challenge-response HMAC-SHA1 d'une YubiKey, sans matériel (aucun test ne doit dépendre
/// d'un périphérique physique). Le secret programmé sur le slot simulé pilote la réponse.
/// </summary>
internal sealed class FakeYubiKeyChallengeResponseClient : IYubiKeyChallengeResponseClient
{
    private readonly byte[] _slotSecret;

    public FakeYubiKeyChallengeResponseClient(byte[]? slotSecret = null)
    {
        _slotSecret = slotSecret ?? RandomNumberGenerator.GetBytes(20);
    }

    public bool Connected { get; set; } = true;

    public bool IsPresent() => Connected;

    public byte[] CalculateHmacSha1Response(byte[] challenge)
    {
        if (!Connected)
        {
            throw new YubiKeyNotPresentException();
        }

        using var hmac = new HMACSHA1(_slotSecret);
        return hmac.ComputeHash(challenge);
    }
}
