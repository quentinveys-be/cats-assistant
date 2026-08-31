using CatsAssistant.App;
using CatsAssistant.App.ViewModels;

namespace CatsAssistant.Tests.App;

public class OutlookConnectionCardViewModelTests
{
    [Fact]
    public void Update_SuccessState_ReportsConnected()
    {
        var viewModel = new OutlookConnectionCardViewModel();

        viewModel.Update(new SyncConnectorState(SyncStatus.Success, DateTimeOffset.UtcNow, null));

        Assert.Equal(SyncStatus.Success, viewModel.Status);
        Assert.Equal("connecté", viewModel.StatusLabel);
    }

    [Fact]
    public void Update_ErrorState_ReportsErrorLabel()
    {
        var viewModel = new OutlookConnectionCardViewModel();

        viewModel.Update(new SyncConnectorState(SyncStatus.Error, null, "boom"));

        Assert.Equal("erreur", viewModel.StatusLabel);
    }
}
