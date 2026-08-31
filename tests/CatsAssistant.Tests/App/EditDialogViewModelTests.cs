using CatsAssistant.App.ViewModels;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.App;

public class EditDialogViewModelTests
{
    private static readonly DateOnly Date = new(2026, 8, 11);

    private static readonly IReadOnlyList<TicketSuggestion> Tickets =
    [
        new("ULISTROIS-3428", "Refonte de l'import CSV", "En cours", "P.ACSICAT01-01-P-0005", "ZS042"),
        new("ULISTROIS-3377", "Correctif pointages", "En revue", "P.ACSICAT01-01-P-0005", "ZS042"),
        new("ULISTROIS-3512", "Ticket sans codes", "À faire", null, null),
    ];

    private static readonly TimeBlock Line = new(
        Date,
        new DateTime(2026, 8, 11, 7, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 8, 11, 8, 30, 0, DateTimeKind.Utc),
        "Daily standup",
        "ULISTROIS-3377",
        "P.ACSICAT01-01-P-0005",
        "ZS042",
        "ULISTROIS-3377 - Correctif pointages",
        1.5,
        TimeBlockStatus.Proposed,
        null);

    private static DateTime Local(int hour, int minute) => new(2026, 8, 11, hour, minute, 0, DateTimeKind.Local);

    private static EditDialogViewModel Gap() => EditDialogViewModel.ForGap(Date, Local(14, 0), Local(15, 45), Tickets);

    // ---------- variantes ----------

    [Fact]
    public void Variants_ExposePrototypeTitlesAndActionLabels()
    {
        var block = EditDialogViewModel.ForCapturedActivity(Date, Local(9, 0), Local(9, 45), "idea64", null, Tickets);
        Assert.Equal("Modifier l'activité capturée", block.Title);
        Assert.Equal("Enregistrer", block.SaveLabel);
        Assert.Equal("Marquer non facturable", block.DeleteLabel);
        Assert.False(block.CanDelete);
        Assert.True(block.HasRange);
        Assert.Contains("idea64", block.Subtitle);

        var range = EditDialogViewModel.ForCatsRange(Date, Local(9, 0), Local(10, 0), "ULISTROIS-3377", null, canDelete: true, Tickets);
        Assert.Equal("Modifier la plage CATS", range.Title);
        Assert.Equal("Supprimer la plage", range.DeleteLabel);
        Assert.True(range.CanDelete);
        Assert.Contains("plage CATS regroupant les segments captés", range.Subtitle);

        var gap = Gap();
        Assert.Equal("Imputer cette plage", gap.Title);
        Assert.Equal("Créer la plage CATS", gap.SaveLabel);
        Assert.Equal("Non facturable", gap.DeleteLabel);
        Assert.Contains("activité non corrélée — aucun ticket détecté", gap.Subtitle);

        var line = EditDialogViewModel.ForCatsLine(1, Line, Tickets);
        Assert.Equal("Modifier la ligne CATS", line.Title);
        Assert.Equal("Supprimer", line.DeleteLabel);
        Assert.True(line.CanDelete);
        Assert.False(line.HasRange);
        Assert.True(line.HasDuration);
        Assert.Contains("durée agrégée des plages du corrélateur", line.Subtitle);
    }

    // ---------- steppers de plage horaire ----------

    [Fact]
    public void RangeSteppers_MoveByFifteenMinutes()
    {
        var gap = Gap();
        Assert.Equal("14:00", gap.StartLabel);
        Assert.Equal("15:45", gap.EndLabel);

        gap.StartPlusCommand.Execute(null);
        gap.EndMinusCommand.Execute(null);

        Assert.Equal("14:15", gap.StartLabel);
        Assert.Equal("15:30", gap.EndLabel);
    }

    [Fact]
    public void RangeSteppers_KeepStartBeforeEnd()
    {
        var gap = EditDialogViewModel.ForGap(Date, Local(14, 0), Local(14, 30), Tickets);

        gap.StartPlusCommand.Execute(null);
        gap.StartPlusCommand.Execute(null); // bloqué : le début doit rester 15 min avant la fin

        Assert.Equal("14:15", gap.StartLabel);

        gap.EndMinusCommand.Execute(null); // bloqué aussi : 14:30 est déjà à début + 15 min
        Assert.Equal("14:30", gap.EndLabel);
    }

    [Fact]
    public void RangeSteppers_ClampToSevenAndTwenty()
    {
        var gap = EditDialogViewModel.ForGap(Date, Local(7, 0), Local(20, 0), Tickets);

        gap.StartMinusCommand.Execute(null);
        gap.EndPlusCommand.Execute(null);

        Assert.Equal("07:00", gap.StartLabel);
        Assert.Equal("20:00", gap.EndLabel);
    }

    [Fact]
    public void RangeHint_ShowsDurationStepAndSapDecimal()
    {
        Assert.Equal("1:45 · pas de 15 min · 1,75 h vers SAP", Gap().RangeHint);
    }

    // ---------- stepper de durée (ligne) ----------

    [Fact]
    public void DurationStepper_MovesByQuarterHourWithinBounds()
    {
        var line = EditDialogViewModel.ForCatsLine(1, Line, Tickets);
        Assert.Equal("1:30", line.DurationLabel);

        line.DurationPlusCommand.Execute(null);
        Assert.Equal("1:45", line.DurationLabel);
        Assert.Contains("(1,75)", line.DurationHint);

        for (var i = 0; i < 20; i++)
        {
            line.DurationMinusCommand.Execute(null);
        }

        Assert.Equal(0.25, line.DurationHours); // plancher 0,25 h

        for (var i = 0; i < 60; i++)
        {
            line.DurationPlusCommand.Execute(null);
        }

        Assert.Equal(12, line.DurationHours); // plafond 12 h
    }

    // ---------- autocomplete des tickets assignés ----------

    [Fact]
    public void Suggestions_FilterOnKeyAndSummaryCaseInsensitive()
    {
        var gap = Gap();

        gap.Query = "3428";
        Assert.Equal("ULISTROIS-3428", Assert.Single(gap.Suggestions).Key);

        gap.Query = "correctif";
        Assert.Equal("ULISTROIS-3377", Assert.Single(gap.Suggestions).Key);

        gap.Query = string.Empty;
        Assert.Equal(3, gap.Suggestions.Count);
    }

    [Fact]
    public void Suggestions_NoMatch_IsExposedWhenListOpen()
    {
        var gap = Gap();

        gap.Query = "introuvable";

        Assert.True(gap.IsListOpen);
        Assert.Empty(gap.Suggestions);
        Assert.True(gap.NoMatch);
    }

    [Fact]
    public void SelectTicket_PrefillsNoteQueryAndCodes()
    {
        var gap = Gap();

        gap.SelectTicket(Tickets[0]);

        Assert.Equal("ULISTROIS-3428", gap.Query);
        Assert.Equal("ULISTROIS-3428 - Refonte de l'import CSV", gap.Note);
        Assert.Equal("P.ACSICAT01-01-P-0005", gap.PosidDisplay);
        Assert.Equal("ZS042", gap.ZwpidDisplay);
        Assert.Equal("À vérifier", gap.VerificationLabel);
        Assert.False(gap.IsListOpen);
    }

    [Fact]
    public void VerificationBadge_WarnsWhenTicketHasNoExtractedCodes()
    {
        var gap = Gap();
        Assert.Equal("aucun ticket", gap.VerificationLabel);

        gap.SelectTicket(Tickets[2]);

        Assert.Equal("—", gap.PosidDisplay);
        Assert.Equal("codes introuvables ⚠", gap.VerificationLabel);
        Assert.True(gap.VerificationIsWarning);
    }

    // ---------- compteur de note ----------

    [Fact]
    public void NoteCounter_TracksLengthWithPrototypeThresholds()
    {
        var gap = Gap();
        Assert.Equal("0 / 80", gap.NoteCounter);
        Assert.Equal(NoteCounterSeverity.Normal, gap.NoteSeverity);

        gap.Note = new string('a', 71);
        Assert.Equal("71 / 80", gap.NoteCounter);
        Assert.Equal(NoteCounterSeverity.Warning, gap.NoteSeverity);

        gap.Note = new string('a', 81);
        Assert.Equal(NoteCounterSeverity.Error, gap.NoteSeverity);
    }

    // ---------- actions ----------

    [Fact]
    public void Gap_CannotSaveWithoutTicket()
    {
        var gap = Gap();
        Assert.False(gap.CanSave);
        Assert.False(gap.SaveCommand.CanExecute(null));

        gap.SelectTicket(Tickets[0]);

        Assert.True(gap.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void SaveAndCancel_SetOutcomeAndRequestClose()
    {
        var line = EditDialogViewModel.ForCatsLine(1, Line, Tickets);
        bool? closeResult = null;
        line.RequestClose += result => closeResult = result;

        line.SaveCommand.Execute(null);
        Assert.Equal(EditDialogOutcome.Saved, line.Outcome);
        Assert.True(closeResult);

        line.CancelCommand.Execute(null);
        Assert.Equal(EditDialogOutcome.Cancelled, line.Outcome);
        Assert.False(closeResult);
    }

    [Fact]
    public void ForCatsLine_PrefillsFieldsFromLine()
    {
        var line = EditDialogViewModel.ForCatsLine(7, Line, Tickets);

        Assert.Equal("ULISTROIS-3377", line.Query);
        Assert.Equal("ULISTROIS-3377 - Correctif pointages", line.Note);
        Assert.Equal("P.ACSICAT01-01-P-0005", line.PosidDisplay);
        Assert.Equal(7, line.LineId);
        Assert.Equal(Line, line.InitialLine);
    }
}
