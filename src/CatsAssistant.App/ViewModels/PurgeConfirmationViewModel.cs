using CatsAssistant.App.Mvvm;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// VM du dialogue "Purger les données locales ?" (issue #23). La saisie exacte de <see cref="ConfirmationPhrase"/>
/// est la seule chose qui active <see cref="PurgeCommand"/> — <see cref="Preview"/> est calculé une fois à
/// l'ouverture pour que les comptes annoncés correspondent exactement à ce que <see cref="Purge"/> effacera.
/// </summary>
public sealed class PurgeConfirmationViewModel : ObservableObject
{
    public const string ConfirmationPhrase = "PURGER";

    private readonly ManualPurgeService _purgeService;
    private string _confirmationText = string.Empty;

    public PurgeConfirmationViewModel(ManualPurgeService purgeService)
    {
        _purgeService = purgeService;
        Preview = purgeService.Preview();

        PurgeCommand = new RelayCommand(Purge, CanPurge);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
    }

    public ManualPurgeSummary Preview { get; }

    public bool IsPurged { get; private set; }

    public string ConfirmationText
    {
        get => _confirmationText;
        set => SetProperty(ref _confirmationText, value);
    }

    public RelayCommand PurgeCommand { get; }

    public RelayCommand CancelCommand { get; }

    public event EventHandler? RequestClose;

    private bool CanPurge(object? parameter) =>
        string.Equals(ConfirmationText, ConfirmationPhrase, StringComparison.Ordinal);

    private void Purge(object? parameter)
    {
        _purgeService.Purge();
        IsPurged = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
