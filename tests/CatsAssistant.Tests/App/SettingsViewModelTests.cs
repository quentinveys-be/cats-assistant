using CatsAssistant.App.ViewModels;

namespace CatsAssistant.Tests.App;

public class SettingsViewModelTests
{
    [Fact]
    public void Constructor_SelectsCaptureTabByDefault()
    {
        var viewModel = new SettingsViewModel();

        Assert.True(viewModel.IsCaptureTabSelected);
        Assert.False(viewModel.IsDataTabSelected);
    }

    [Fact]
    public void SelectDataTabCommand_SwitchesTabs()
    {
        var viewModel = new SettingsViewModel();

        viewModel.SelectDataTabCommand.Execute(null);

        Assert.False(viewModel.IsCaptureTabSelected);
        Assert.True(viewModel.IsDataTabSelected);

        viewModel.SelectCaptureTabCommand.Execute(null);

        Assert.True(viewModel.IsCaptureTabSelected);
        Assert.False(viewModel.IsDataTabSelected);
    }
}
