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
                Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
            }

            Assert.True(File.Exists(path));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
