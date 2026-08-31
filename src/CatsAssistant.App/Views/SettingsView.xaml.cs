using System.Windows;
using CatsAssistant.App.ViewModels;

namespace CatsAssistant.App.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnPurgeClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel || viewModel.Data.PurgeService is not { } purgeService)
        {
            return;
        }

        var dialog = new PurgeConfirmationDialog(new PurgeConfirmationViewModel(purgeService))
        {
            Owner = Window.GetWindow(this),
        };
        dialog.ShowDialog();

        viewModel.Data.RefreshDatabaseInfo();
    }
}
