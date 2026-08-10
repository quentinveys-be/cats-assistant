using System.Globalization;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Store;

public sealed class SqliteActivityEventRepository : IActivityEventRepository
{
    private readonly SqliteConnection _connection;

    public SqliteActivityEventRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public long Insert(DateTime timestampUtc, ActivityEventKind kind, string? process, string? windowTitle, string? url)
    {
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO activity_events (ts, kind, process, window_title, url)
                VALUES ($ts, $kind, $process, $windowTitle, $url);
                """;
            command.Parameters.AddWithValue("$ts", FormatTimestamp(timestampUtc));
            command.Parameters.AddWithValue("$kind", ToDbValue(kind));
            command.Parameters.AddWithValue("$process", (object?)process ?? DBNull.Value);
            command.Parameters.AddWithValue("$windowTitle", (object?)windowTitle ?? DBNull.Value);
            command.Parameters.AddWithValue("$url", (object?)url ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        using var idCommand = _connection.CreateCommand();
        idCommand.CommandText = "SELECT last_insert_rowid();";
        return (long)idCommand.ExecuteScalar()!;
    }

    public IReadOnlyList<ActivityEvent> GetByDateRange(DateTime fromUtc, DateTime toUtc)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT id, ts, kind, process, window_title, url
            FROM activity_events
            WHERE ts >= $from AND ts < $to
            ORDER BY ts;
            """;
        command.Parameters.AddWithValue("$from", FormatTimestamp(fromUtc));
        command.Parameters.AddWithValue("$to", FormatTimestamp(toUtc));

        var results = new List<ActivityEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadEvent(reader));
        }

        return results;
    }

    public void Delete(long id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM activity_events WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public int DeleteOlderThan(DateTime thresholdUtc)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM activity_events WHERE ts < $threshold;";
        command.Parameters.AddWithValue("$threshold", FormatTimestamp(thresholdUtc));
        return command.ExecuteNonQuery();
    }

    private static ActivityEvent ReadEvent(SqliteDataReader reader)
    {
        return new ActivityEvent(
            reader.GetInt64(0),
            ParseTimestamp(reader.GetString(1)),
            ParseKind(reader.GetString(2)),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }

    private static string FormatTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);

    private static DateTime ParseTimestamp(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string ToDbValue(ActivityEventKind kind) => kind switch
    {
        ActivityEventKind.Foreground => "foreground",
        ActivityEventKind.IdleStart => "idle_start",
        ActivityEventKind.IdleEnd => "idle_end",
        ActivityEventKind.TitleChange => "title_change",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static ActivityEventKind ParseKind(string value) => value switch
    {
        "foreground" => ActivityEventKind.Foreground,
        "idle_start" => ActivityEventKind.IdleStart,
        "idle_end" => ActivityEventKind.IdleEnd,
        "title_change" => ActivityEventKind.TitleChange,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
