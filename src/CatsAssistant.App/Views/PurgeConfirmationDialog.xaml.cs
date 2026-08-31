using System.Windows;
using CatsAssistant.App.ViewModels;

namespace CatsAssistant.App.Views;

public partial class PurgeConfirmationDialog : Window
{
    private readonly PurgeConfirmationViewModel _viewModel;

    public PurgeConfirmationDialog(PurgeConfirmationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.RequestClose += OnRequestClose;
        Closed += (_, _) => _viewModel.RequestClose -= OnRequestClose;
    }

    private void OnRequestClose(object? sender, EventArgs e) => Close();
}
