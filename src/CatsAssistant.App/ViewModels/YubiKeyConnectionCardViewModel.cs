using System.Windows.Threading;
using CatsAssistant.App.Mvvm;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Carte "Connexions" du coffre YubiKey (issue #24) : "Tester la clé" réutilise
/// <see cref="YubiKeyVaultCoordinator.TryUnlock"/> (même dérivation que le dialogue de déverrouillage,
/// issue #26) plutôt que de dupliquer la logique de challenge-response.
/// </summary>
public sealed class YubiKeyConnectionCardViewModel : ObservableObject
{
    private readonly YubiKeyVaultCoordinator _coordinator;
    private readonly Dispatcher _dispatcher;
    private bool _isTesting;
    private string? _testResultMessage;

    public YubiKeyConnectionCardViewModel(YubiKeyVaultCoordinator coordinator)
    {
        _coordinator = coordinator;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _coordinator.StateChanged += (_, _) => _dispatcher.BeginInvoke(() =>
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusLabel));
        });

        TestKeyCommand = new RelayCommand(() => _ = TestKeyAsync(), () => !IsTesting);
    }

    public RelayCommand TestKeyCommand { get; }

    public YubiKeyVaultState Status => _coordinator.State;

    public string StatusLabel => Status switch
    {
        YubiKeyVaultState.Unlocked => "déverrouillé",
        YubiKeyVaultState.Degraded => "mode dégradé",
        _ => "verrouillé",
    };

    public bool IsTesting
    {
        get => _isTesting;
        private set => SetProperty(ref _isTesting, value);
    }

    public string? TestResultMessage
    {
        get => _testResultMessage;
        private set => SetProperty(ref _testResultMessage, value);
    }

    public async Task TestKeyAsync()
    {
        if (IsTesting)
        {
            return;
        }

        IsTesting = true;
        TestResultMessage = null;

        var unlocked = await Task.Run(() => _coordinator.TryUnlock());

        IsTesting = false;
        TestResultMessage = unlocked
            ? "Clé vérifiée avec succès."
            : _coordinator.IsYubiKeyPresent
                ? "Appui refusé ou délai dépassé. Réessayez."
                : "Aucune YubiKey détectée.";
    }
}
