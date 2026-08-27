using Microsoft.Data.Sqlite;

namespace CatsAssistant.Store;

public sealed class SqliteSettingsRepository : ISettingsRepository
{
    private readonly SqliteConnection _connection;

    public SqliteSettingsRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public string? Get(string key)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE \"key\" = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    public void Set(string key, string value)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings ("key", value) VALUES ($key, $value)
            ON CONFLICT("key") DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }
}
