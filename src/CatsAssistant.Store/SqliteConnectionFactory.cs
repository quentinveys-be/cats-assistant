using Microsoft.Data.Sqlite;

namespace CatsAssistant.Store;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;
    private readonly SqliteMigrator _migrator;

    public SqliteConnectionFactory(string databasePath, string? key = null, SqliteMigrator? migrator = null)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = databasePath };
        if (!string.IsNullOrEmpty(key))
        {
            connectionStringBuilder.Password = key;
        }

        _connectionString = connectionStringBuilder.ToString();
        _migrator = migrator ?? new SqliteMigrator();
    }

    public static string GetDefaultDatabasePath()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatsAssistant");
        return Path.Combine(dataDirectory, "cats-assistant.db");
    }

    public static string GetDefaultActivityDatabasePath()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatsAssistant");
        return Path.Combine(dataDirectory, "activity.db");
    }

    public static string GetDefaultBusinessDatabasePath()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatsAssistant");
        return Path.Combine(dataDirectory, "business.db");
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            _migrator.Migrate(connection);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
