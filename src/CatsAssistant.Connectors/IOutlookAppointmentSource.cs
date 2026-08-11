namespace CatsAssistant.Connectors;

/// <summary>
/// Low-level access to the local Outlook calendar via COM automation. Isolated behind this interface so
/// <see cref="OutlookComConnector"/> can be unit tested with a fake source instead of a real Outlook
/// instance (docs/phases.md, step 2.4).
/// </summary>
public interface IOutlookAppointmentSource
{
    /// <summary>
    /// Returns appointments starting within [fromLocal, toLocal), in the local system time zone.
    /// </summary>
    IReadOnlyList<OutlookAppointmentSnapshot> GetAppointments(DateTime fromLocal, DateTime toLocal);
}
