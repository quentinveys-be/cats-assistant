using System.Globalization;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Store;

public sealed class SqliteActivityEventRepository : IActivityEventRepository
{
    private readonly SqliteConnection _connection;

    // The collector writes from the WinEvent callback thread and from the idle-poll timer thread while the UI
    // reads on the dispatcher thread, but a SqliteConnection serves one caller at a time. Serialise here rather
    // than at every call site — SQLite writes on a local file are short enough that contention stays invisible.
    private readonly object _gate = new();

    public SqliteActivityEventRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public long Insert(DateTime timestampUtc, ActivityEventKind kind, string? process, string? windowTitle, string? url)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO activity_events (ts, kind, process, window_title, url)
                VALUES ($ts, $kind, $process, $windowTitle, $url)
                RETURNING id;
                """;
            command.Parameters.AddWithValue("$ts", FormatTimestamp(timestampUtc));
            command.Parameters.AddWithValue("$kind", ToDbValue(kind));
            command.Parameters.AddWithValue("$process", (object?)process ?? DBNull.Value);
            command.Parameters.AddWithValue("$windowTitle", (object?)windowTitle ?? DBNull.Value);
            command.Parameters.AddWithValue("$url", (object?)url ?? DBNull.Value);
            return (long)command.ExecuteScalar()!;
        }
    }

    public IReadOnlyList<ActivityEvent> GetByDateRange(DateTime fromUtc, DateTime toUtc)
    {
        lock (_gate)
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
    }

    public void Delete(long id)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM activity_events WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }

    public int DeleteOlderThan(DateTime thresholdUtc)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM activity_events WHERE ts < $threshold;";
            command.Parameters.AddWithValue("$threshold", FormatTimestamp(thresholdUtc));
            return command.ExecuteNonQuery();
        }
    }

    public int Count()
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM activity_events;";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public int DeleteAll()
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM activity_events;";
            return command.ExecuteNonQuery();
        }
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
