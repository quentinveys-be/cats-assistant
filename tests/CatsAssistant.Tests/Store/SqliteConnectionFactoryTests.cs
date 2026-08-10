using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class SqliteConnectionFactoryTests
{
    [Fact]
    public void OpenConnection_CreatesDatabaseFileAndAppliesMigrations()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cats-assistant-tests-{Guid.NewGuid():N}.db");

        try
        {
            var factory = new SqliteConnectionFactory(path);

            using (var connection = factory.OpenConnection())
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
                var version = Convert.ToInt32(command.ExecuteScalar());

                // La factory doit avoir joué les migrations ; leur contenu est couvert par SqliteMigratorTests.
                Assert.True(version >= 1, $"aucune migration appliquée (version = {version})");
            }

            Assert.True(File.Exists(path));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void OpenConnection_WithKey_ReopensWithSameKey()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cats-assistant-tests-{Guid.NewGuid():N}.db");
        const string key = "correct-horse-battery-staple";

        try
        {
            using (var connection = new SqliteConnectionFactory(path, key).OpenConnection())
            {
                Assert.True(GetSchemaVersion(connection) >= 1);
            }

            SqliteConnection.ClearAllPools();

            using var reopened = new SqliteConnectionFactory(path, key).OpenConnection();
            Assert.True(GetSchemaVersion(reopened) >= 1);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void OpenConnection_WithWrongKey_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cats-assistant-tests-{Guid.NewGuid():N}.db");

        try
        {
            using (new SqliteConnectionFactory(path, "correct-key").OpenConnection())
            {
            }

            SqliteConnection.ClearAllPools();

            Assert.ThrowsAny<SqliteException>(() =>
            {
                using var connection = new SqliteConnectionFactory(path, "wrong-key").OpenConnection();
            });
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void OpenConnection_WithoutKey_OnEncryptedDatabase_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cats-assistant-tests-{Guid.NewGuid():N}.db");

        try
        {
            using (new SqliteConnectionFactory(path, "correct-key").OpenConnection())
            {
            }

            SqliteConnection.ClearAllPools();

            Assert.ThrowsAny<SqliteException>(() =>
            {
                using var connection = new SqliteConnectionFactory(path).OpenConnection();
            });
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void OpenConnection_TwoMigrationSets_ApplyIndependentlyOnTwoFiles()
    {
        var activityPath = Path.Combine(Path.GetTempPath(), $"cats-assistant-tests-activity-{Guid.NewGuid():N}.db");
        var businessPath = Path.Combine(Path.GetTempPath(), $"cats-assistant-tests-business-{Guid.NewGuid():N}.db");

        try
        {
            using var activityConnection = new SqliteConnectionFactory(
                    activityPath, migrator: new SqliteMigrator(SqliteMigrator.ActivityMigrations))
                .OpenConnection();
            using var businessConnection = new SqliteConnectionFactory(
                    businessPath, migrator: new SqliteMigrator(SqliteMigrator.BusinessMigrations))
                .OpenConnection();

            Assert.Equal(2, GetSchemaVersion(activityConnection));
            Assert.Equal(0, GetSchemaVersion(businessConnection));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(activityPath);
            File.Delete(businessPath);
        }
    }

    private static int GetSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
