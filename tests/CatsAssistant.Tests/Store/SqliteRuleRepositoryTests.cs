using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class SqliteRuleRepositoryTests
{
    private static readonly Rule SampleRule = new(
        RuleMatcherKind.TitleRegex,
        @"ULISTROIS[-/](\d+)",
        "ULISTROIS-<n>",
        1,
        RuleOrigin.Manual);

    [Fact]
    public void Insert_ThenGetAll_ReturnsStoredRule()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteRuleRepository(connection);

        var id = repository.Insert(SampleRule);

        Assert.True(id > 0);
        var stored = Assert.Single(repository.GetAll());
        Assert.Equal(id, stored.Id);
        Assert.Equal(SampleRule, stored.Rule);
    }

    [Fact]
    public void GetAll_OrdersByPriority()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteRuleRepository(connection);

        repository.Insert(SampleRule with { MatcherValue = "second", Priority = 2 });
        repository.Insert(SampleRule with { MatcherValue = "first", Priority = 1 });

        var all = repository.GetAll();

        Assert.Equal(new[] { "first", "second" }, all.Select(r => r.Rule.MatcherValue));
    }

    [Fact]
    public void Update_OverwritesFields()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteRuleRepository(connection);
        var id = repository.Insert(SampleRule);

        var edited = SampleRule with { Priority = 5, Origin = RuleOrigin.Learned };
        repository.Update(id, edited);

        var stored = Assert.Single(repository.GetAll());
        Assert.Equal(edited, stored.Rule);
    }

    [Fact]
    public void Update_UnknownId_Throws()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteRuleRepository(connection);

        Assert.Throws<KeyNotFoundException>(() => repository.Update(999, SampleRule));
    }

    [Fact]
    public void Delete_UnknownId_Throws()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteRuleRepository(connection);

        Assert.Throws<KeyNotFoundException>(() => repository.Delete(999));
    }

    [Fact]
    public void Delete_RemovesRule()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteRuleRepository(connection);
        var id = repository.Insert(SampleRule);

        repository.Delete(id);

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
