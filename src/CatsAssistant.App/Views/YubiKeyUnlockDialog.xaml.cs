using System.Windows;
using CatsAssistant.App.ViewModels;

namespace CatsAssistant.App.Views;

public partial class YubiKeyUnlockDialog : Window
{
    private readonly YubiKeyUnlockViewModel _viewModel;

    public YubiKeyUnlockDialog(YubiKeyUnlockViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.RequestClose += OnRequestClose;
        Closed += (_, _) => _viewModel.RequestClose -= OnRequestClose;
        Loaded += (_, _) => _ = _viewModel.RetryAsync();
    }

    private void OnRequestClose(object? sender, EventArgs e) => Close();
}
