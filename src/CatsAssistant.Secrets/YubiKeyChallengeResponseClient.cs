using Yubico.YubiKey;
using Yubico.YubiKey.Otp;

namespace CatsAssistant.Secrets;

/// <summary>
/// Adaptateur réel vers le SDK Yubico.YubiKey (application OTP, credential HMAC-SHA1 programmé sur le
/// slot indiqué). Nécessite un slot enrôlé en HMAC-SHA1 challenge-response (action humaine, docs/phases.md 2.1).
/// </summary>
public sealed class YubiKeyChallengeResponseClient : IYubiKeyChallengeResponseClient
{
    private readonly Slot _slot;

    public YubiKeyChallengeResponseClient(Slot slot = Slot.ShortPress)
    {
        _slot = slot;
    }

    public bool IsPresent()
    {
        return YubiKeyDevice.FindAll().Any();
    }

    public byte[] CalculateHmacSha1Response(byte[] challenge)
    {
        var device = YubiKeyDevice.FindAll().FirstOrDefault()
            ?? throw new YubiKeyNotPresentException();

        using var otpSession = new OtpSession(device);
        var response = otpSession.CalculateChallengeResponse(_slot)
            .UseChallenge(challenge)
            .UseYubiOtp(false)
            .GetDataBytes();

        return response.ToArray();
    }
}
