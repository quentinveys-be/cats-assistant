using System.Runtime.InteropServices;

namespace CatsAssistant.Connectors;

/// <summary>
/// Reads appointments from the local Outlook profile via late-bound COM automation
/// (Outlook.Application → MAPI namespace → default calendar folder). Only Subject, Organizer, Start and
/// End are ever read — Body/RTFBody/attachments are never touched (CLAUDE.md: no meeting content
/// capture). Kept thin and COM-specific on purpose; behavior is exercised through
/// <see cref="OutlookComConnector"/> tests against a fake <see cref="IOutlookAppointmentSource"/>, since
/// this class itself can only run against a real Outlook installation.
/// </summary>
public sealed class OutlookComAppointmentSource : IOutlookAppointmentSource
{
    private const string OutlookProgId = "Outlook.Application";
    private const int OlFolderCalendar = 9;
    private const int OlAppointmentItemClass = 26;

    public IReadOnlyList<OutlookAppointmentSnapshot> GetAppointments(DateTime fromLocal, DateTime toLocal) =>
        StaThreadRunner.Run(() => GetAppointmentsOnStaThread(fromLocal, toLocal));

    private static IReadOnlyList<OutlookAppointmentSnapshot> GetAppointmentsOnStaThread(DateTime fromLocal, DateTime toLocal)
    {
        object? outlookApplication = null;
        object? outlookNamespace = null;
        object? calendarFolder = null;
        object? items = null;
        object? restrictedItems = null;

        try
        {
            outlookApplication = AttachOrStartOutlook();
            outlookNamespace = OutlookComInterop.InvokeMethod(outlookApplication, "GetNamespace", "MAPI")
                ?? throw new OutlookUnavailableException("Impossible d'ouvrir le namespace MAPI d'Outlook.");
            calendarFolder = OutlookComInterop.InvokeMethod(outlookNamespace, "GetDefaultFolder", OlFolderCalendar)
                ?? throw new OutlookUnavailableException("Dossier calendrier Outlook introuvable.");
            items = OutlookComInterop.GetProperty(calendarFolder, "Items")
                ?? throw new OutlookUnavailableException("Impossible de lire les éléments du calendrier Outlook.");

            // Recurring meetings are stored as a single master item; expanding recurrences is required to
            // get one occurrence per actual meeting, and Restrict only works on an expanded/sorted range.
            OutlookComInterop.SetProperty(items, "IncludeRecurrences", true);
            OutlookComInterop.InvokeMethod(items, "Sort", "[Start]");

            var filter = OutlookRestrictFilterBuilder.BuildStartDateRangeFilter(fromLocal, toLocal);
            restrictedItems = OutlookComInterop.InvokeMethod(items, "Restrict", filter)
                ?? throw new OutlookUnavailableException("Le filtrage du calendrier Outlook a échoué.");

            return ReadAppointments(restrictedItems);
        }
        catch (COMException ex)
        {
            throw new OutlookUnavailableException(
                $"Outlook local est indisponible ou n'a pas de profil configuré ({ex.Message}, HRESULT 0x{ex.HResult:X8}).", ex);
        }
        finally
        {
            OutlookComInterop.Release(restrictedItems);
            OutlookComInterop.Release(items);
            OutlookComInterop.Release(calendarFolder);
            OutlookComInterop.Release(outlookNamespace);
            // outlookApplication is intentionally not released: it may be the user's already-running
            // Outlook instance, and releasing it here could tear it down from under them.
        }
    }

    private static List<OutlookAppointmentSnapshot> ReadAppointments(object restrictedItems)
    {
        var results = new List<OutlookAppointmentSnapshot>();
        var current = OutlookComInterop.InvokeMethod(restrictedItems, "GetFirst");

        while (current is not null)
        {
            object? next = null;
            try
            {
                if (Convert.ToInt32(OutlookComInterop.GetProperty(current, "Class")) == OlAppointmentItemClass)
                {
                    var subject = OutlookComInterop.GetProperty(current, "Subject") as string;
                    var organizer = OutlookComInterop.GetProperty(current, "Organizer") as string;
                    var start = (DateTime)OutlookComInterop.GetProperty(current, "Start")!;
                    var end = (DateTime)OutlookComInterop.GetProperty(current, "End")!;

                    results.Add(new OutlookAppointmentSnapshot(start, end, subject, organizer));
                }

                next = OutlookComInterop.InvokeMethod(restrictedItems, "GetNext");
            }
            finally
            {
                OutlookComInterop.Release(current);
            }

            current = next;
        }

        return results;
    }

    private static object AttachOrStartOutlook()
    {
        var outlookType = Type.GetTypeFromProgID(OutlookProgId)
            ?? throw new OutlookUnavailableException("Outlook n'est pas installé sur ce poste.");

        // Outlook.Application is a single-instance COM server: when Outlook is already running,
        // CreateInstance attaches to that instance instead of launching a second one.
        return Activator.CreateInstance(outlookType)
            ?? throw new OutlookUnavailableException("Impossible de démarrer Outlook.");
    }
}
