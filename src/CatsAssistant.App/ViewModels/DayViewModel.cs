namespace CatsAssistant.App.ViewModels;

public sealed class DayViewModel() : ScreenViewModelBase("Journée")
{
    /// <summary>Jour ciblé par la navigation "Ouvrir la journée" du Rattrapage (issue #22). La timeline
    /// elle-même reste hors périmètre tant que cet écran n'est pas implémenté.</summary>
    public DateOnly? SelectedDate { get; set; }
}
