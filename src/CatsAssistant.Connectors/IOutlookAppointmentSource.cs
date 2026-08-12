namespace CatsAssistant.Connectors;

/// <summary>
/// Low-level access to the local Outlook calendar via COM automation. Isolated behind this interface so
/// <see cref="OutlookComConnector"/> can be unit tested with a fake source instead of a real Outlook
/// instance (docs/phases.md, step 2.4).
/// </summary>
/// <remarks>
/// Real (COM-backed) implementations require an STA thread — Outlook's automation server throws
/// RPC_E_WRONG_THREAD when called from an MTA/thread-pool thread. <see cref="OutlookComAppointmentSource"/>
/// guarantees this itself via <see cref="StaThreadRunner"/>, so callers of this interface do not need to
/// manage apartment state; a fake implementation used in tests has no such constraint.
/// </remarks>
public interface IOutlookAppointmentSource
{
    /// <summary>
    /// Returns appointments starting within [fromLocal, toLocal), in the local system time zone.
    /// </summary>
    IReadOnlyList<OutlookAppointmentSnapshot> GetAppointments(DateTime fromLocal, DateTime toLocal);
}
