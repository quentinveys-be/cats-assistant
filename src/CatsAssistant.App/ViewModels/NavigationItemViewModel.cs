using CatsAssistant.App.Mvvm;

namespace CatsAssistant.App.ViewModels;

/// <summary>Une entrée du rail de navigation (issue #15). <see cref="Screen"/> identifie l'écran ciblé.</summary>
public sealed class NavigationItemViewModel : ObservableObject
{
    private bool _isSelected;
    private int? _badgeCount;

    public NavigationItemViewModel(string label, ScreenViewModelBase screen, Action<NavigationItemViewModel> select, int? badgeCount = null)
    {
        Label = label;
        Screen = screen;
        _badgeCount = badgeCount;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public string Label { get; }

    public ScreenViewModelBase Screen { get; }

    public RelayCommand SelectCommand { get; }

    public int? BadgeCount
    {
        get => _badgeCount;
        set => SetProperty(ref _badgeCount, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
