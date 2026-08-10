using Microsoft.Data.Sqlite;

namespace CatsAssistant.Store;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;
    private readonly SqliteMigrator _migrator;

    public SqliteConnectionFactory(string databasePath, SqliteMigrator? migrator = null)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        _migrator = migrator ?? new SqliteMigrator();
    }

    public static string GetDefaultDatabasePath()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatsAssistant");
        return Path.Combine(dataDirectory, "cats-assistant.db");
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        _migrator.Migrate(connection);
        return connection;
    }
}
