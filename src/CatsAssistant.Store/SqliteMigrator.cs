using Microsoft.Data.Sqlite;

namespace CatsAssistant.Store;

public sealed class SqliteMigrator
{
    public static readonly IReadOnlyList<(int Version, string FileName)> ActivityMigrations =
    [
        (1, "0001_initial_schema.sql"),
        (2, "0002_activity_events_ts_index.sql"),
    ];

    public static readonly IReadOnlyList<(int Version, string FileName)> BusinessMigrations = [];

    private readonly IReadOnlyList<(int Version, string FileName)> _migrations;

    public SqliteMigrator(IReadOnlyList<(int Version, string FileName)>? migrations = null)
    {
        _migrations = migrations ?? ActivityMigrations;
    }

    public void Migrate(SqliteConnection connection)
    {
        EnsureSchemaVersionTable(connection);
        var currentVersion = GetCurrentVersion(connection);

        foreach (var migration in _migrations.Where(m => m.Version > currentVersion).OrderBy(m => m.Version))
        {
            ApplyMigration(connection, migration.Version, migration.FileName);
        }
    }

    private static void EnsureSchemaVersionTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private static int GetCurrentVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void ApplyMigration(SqliteConnection connection, int version, string fileName)
    {
        var script = ReadMigrationScript(fileName);

        using var transaction = connection.BeginTransaction();

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = script;
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO schema_version (version, applied_at) VALUES ($version, $appliedAt);";
            command.Parameters.AddWithValue("$version", version);
            command.Parameters.AddWithValue("$appliedAt", DateTime.UtcNow.ToString("o"));
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static string ReadMigrationScript(string fileName)
    {
        var assembly = typeof(SqliteMigrator).Assembly;
        var resourceName = $"{typeof(SqliteMigrator).Namespace}.Migrations.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Script de migration introuvable : {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
