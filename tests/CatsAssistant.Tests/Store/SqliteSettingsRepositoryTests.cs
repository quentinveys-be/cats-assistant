using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class SqliteSettingsRepositoryTests
{
    [Fact]
    public void Get_UnknownKey_ReturnsNull()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteSettingsRepository(connection);

        Assert.Null(repository.Get("ui.theme"));
    }

    [Fact]
    public void Set_ThenGet_ReturnsStoredValue()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteSettingsRepository(connection);

        repository.Set("ui.theme", "dark");

        Assert.Equal("dark", repository.Get("ui.theme"));
    }

    [Fact]
    public void Set_ExistingKey_OverwritesValue()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteSettingsRepository(connection);

        repository.Set("ui.theme", "dark");
        repository.Set("ui.theme", "light");

        Assert.Equal("light", repository.Get("ui.theme"));
    }

    private static SqliteConnection OpenMigratedConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        new SqliteMigrator(SqliteMigrator.ActivityMigrations).Migrate(connection);
        return connection;
    }
}
