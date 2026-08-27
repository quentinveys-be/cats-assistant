using CatsAssistant.App.Mvvm;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// VM du dialogue « Touchez votre YubiKey » (issue #26). <see cref="RetryAsync"/> délègue la dérivation
/// bloquante (attend le touch physique) à un thread de fond — jamais sur le thread UI, sous peine de geler
/// le dialogue pendant tout le timeout du SDK.
/// </summary>
public sealed class YubiKeyUnlockViewModel : ObservableObject
{
    private readonly YubiKeyVaultCoordinator _coordinator;
    private bool _isBusy;
    private string? _errorMessage;

    public YubiKeyUnlockViewModel(YubiKeyVaultCoordinator coordinator)
    {
        _coordinator = coordinator;
        RetryCommand = new RelayCommand(() => _ = RetryAsync(), () => !IsBusy);
        ContinueWithoutVaultCommand = new RelayCommand(ContinueWithoutVault, () => !IsBusy);
    }

    public RelayCommand RetryCommand { get; }

    public RelayCommand ContinueWithoutVaultCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Levé quand le dialogue doit se fermer : coffre déverrouillé ou mode dégradé choisi. Jamais sur un simple échec.</summary>
    public event EventHandler? RequestClose;

    public async Task RetryAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        var unlocked = await Task.Run(() => _coordinator.TryUnlock());

        IsBusy = false;

        if (unlocked)
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            ErrorMessage = _coordinator.IsYubiKeyPresent
                ? "Appui refusé ou délai dépassé. Réessayez."
                : "Aucune YubiKey détectée. Branchez-la puis réessayez.";
        }
    }

    private void ContinueWithoutVault()
    {
        _coordinator.ContinueWithoutVault();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
