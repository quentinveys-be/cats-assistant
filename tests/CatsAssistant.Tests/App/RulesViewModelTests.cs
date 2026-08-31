using CatsAssistant.App.ViewModels;
using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.App;

public class RulesViewModelTests
{
    [Fact]
    public void Constructor_LoadsExistingRulesFromRepository()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteRuleRepository(connection);
        repository.Insert(new Rule(RuleMatcherKind.Process, "chrome.exe", "ULISTROIS-1", 1, RuleOrigin.Manual));

        var viewModel = new RulesViewModel(repository);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("chrome.exe", row.MatcherValue);
        Assert.False(row.IsEditing);
    }

    [Fact]
    public void AddRuleCommand_AddsEditableRow()
    {
        using var connection = OpenMigratedConnection();
        var viewModel = new RulesViewModel(new SqliteRuleRepository(connection));

        viewModel.AddRuleCommand.Execute(null);

        var row = Assert.Single(viewModel.Rows);
        Assert.True(row.IsNew);
        Assert.True(row.IsEditing);
        Assert.Equal(RuleOrigin.Manual, row.Origin);
    }

    [Fact]
    public void SaveCommand_OnNewRow_PersistsToRepositoryAndAppliesAtNextCorrelation()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteRuleRepository(connection);
        var viewModel = new RulesViewModel(repository);
        viewModel.AddRuleCommand.Execute(null);
        var row = viewModel.Rows[0];
        row.MatcherValue = "ULISTROIS[-/](\\d+)";
        row.Target = "ULISTROIS-<n>";
        row.Priority = 3;

        row.SaveCommand.Execute(null);

        Assert.False(row.IsNew);
        Assert.False(row.IsEditing);
        // RuleEvaluator relit IRuleRepository.GetAll() à chaque appel (aucun cache) : une fois persistée
        // ici, la règle est donc automatiquement visible à la prochaine corrélation.
        var stored = Assert.Single(repository.GetAll());
        Assert.Equal("ULISTROIS-<n>", stored.Rule.Target);
        Assert.Equal(3, stored.Rule.Priority);
    }

    [Fact]
    public void SaveCommand_CanExecute_RequiresMatcherValueAndTarget()
    {
        using var connection = OpenMigratedConnection();
        var viewModel = new RulesViewModel(new SqliteRuleRepository(connection));
        viewModel.AddRuleCommand.Execute(null);
        var row = viewModel.Rows[0];

        Assert.False(row.SaveCommand.CanExecute(null));

        row.MatcherValue = "chrome.exe";
        row.Target = "ULISTROIS-1";

        Assert.True(row.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void CancelCommand_OnNewRow_RemovesItWithoutPersisting()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteRuleRepository(connection);
        var viewModel = new RulesViewModel(repository);
        viewModel.AddRuleCommand.Execute(null);

        viewModel.Rows[0].CancelCommand.Execute(null);

        Assert.Empty(viewModel.Rows);
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void EditCommand_ThenSave_UpdatesRepository()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteRuleRepository(connection);
        repository.Insert(new Rule(RuleMatcherKind.Process, "chrome.exe", "ULISTROIS-1", 1, RuleOrigin.Manual));
        var viewModel = new RulesViewModel(repository);
        var row = viewModel.Rows[0];

        row.EditCommand.Execute(null);
        row.Priority = 9;
        row.SaveCommand.Execute(null);

        Assert.False(row.IsEditing);
        var stored = Assert.Single(repository.GetAll());
        Assert.Equal(9, stored.Rule.Priority);
    }

    [Fact]
    public void CancelCommand_OnExistingRow_RevertsUnsavedEdits()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteRuleRepository(connection);
        repository.Insert(new Rule(RuleMatcherKind.Process, "chrome.exe", "ULISTROIS-1", 1, RuleOrigin.Manual));
        var viewModel = new RulesViewModel(repository);
        var row = viewModel.Rows[0];

        row.EditCommand.Execute(null);
        row.MatcherValue = "edge.exe";
        row.CancelCommand.Execute(null);

        Assert.False(row.IsEditing);
        Assert.Equal("chrome.exe", row.MatcherValue);
    }

    [Fact]
    public void DeleteCommand_RemovesFromRepositoryAndRows()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteRuleRepository(connection);
        repository.Insert(new Rule(RuleMatcherKind.Process, "chrome.exe", "ULISTROIS-1", 1, RuleOrigin.Manual));
        var viewModel = new RulesViewModel(repository);

        viewModel.Rows[0].DeleteCommand.Execute(null);

        Assert.Empty(viewModel.Rows);
        Assert.Empty(repository.GetAll());
    }

    private static SqliteConnection OpenMigratedConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        new SqliteMigrator(SqliteMigrator.BusinessMigrations).Migrate(connection);
        return connection;
    }
}
