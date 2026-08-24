using System.Globalization;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Store;

public sealed class SqliteTimeBlockRepository : ITimeBlockRepository
{
    private readonly SqliteConnection _connection;
    private readonly object _gate = new();

    public SqliteTimeBlockRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public long Insert(TimeBlock timeBlock)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO time_blocks (date, "start", "end", source_summary, jira_key, posid, zwpid, note, duration_hours, status, sap_counter)
                VALUES ($date, $start, $end, $sourceSummary, $jiraKey, $posid, $zwpid, $note, $durationHours, $status, $sapCounter)
                RETURNING id;
                """;
            BindParameters(command, timeBlock);
            return (long)command.ExecuteScalar()!;
        }
    }

    public void Update(long id, TimeBlock timeBlock)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                UPDATE time_blocks
                SET date = $date, "start" = $start, "end" = $end, source_summary = $sourceSummary,
                    jira_key = $jiraKey, posid = $posid, zwpid = $zwpid, note = $note,
                    duration_hours = $durationHours, status = $status, sap_counter = $sapCounter
                WHERE id = $id;
                """;
            BindParameters(command, timeBlock);
            command.Parameters.AddWithValue("$id", id);
            if (command.ExecuteNonQuery() == 0)
            {
                throw new KeyNotFoundException($"time_blocks.id={id} introuvable");
            }
        }
    }

    public TimeBlockRow? GetById(long id)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT id, date, "start", "end", source_summary, jira_key, posid, zwpid, note, duration_hours, status, sap_counter
                FROM time_blocks
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", id);

            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadRow(reader) : null;
        }
    }

    public IReadOnlyList<TimeBlockRow> GetByDateRange(DateOnly fromDate, DateOnly toDate)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT id, date, "start", "end", source_summary, jira_key, posid, zwpid, note, duration_hours, status, sap_counter
                FROM time_blocks
                WHERE date >= $from AND date <= $to
                ORDER BY date, "start";
                """;
            command.Parameters.AddWithValue("$from", FormatDate(fromDate));
            command.Parameters.AddWithValue("$to", FormatDate(toDate));

            var results = new List<TimeBlockRow>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(ReadRow(reader));
            }

            return results;
        }
    }

    private static void BindParameters(SqliteCommand command, TimeBlock timeBlock)
    {
        command.Parameters.AddWithValue("$date", FormatDate(timeBlock.Date));
        command.Parameters.AddWithValue("$start", FormatTimestamp(timeBlock.StartUtc));
        command.Parameters.AddWithValue("$end", FormatTimestamp(timeBlock.EndUtc));
        command.Parameters.AddWithValue("$sourceSummary", timeBlock.SourceSummary);
        command.Parameters.AddWithValue("$jiraKey", (object?)timeBlock.JiraKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$posid", timeBlock.Posid);
        command.Parameters.AddWithValue("$zwpid", timeBlock.Zwpid);
        command.Parameters.AddWithValue("$note", timeBlock.Note);
        command.Parameters.AddWithValue("$durationHours", timeBlock.DurationHours);
        command.Parameters.AddWithValue("$status", FormatStatus(timeBlock.Status));
        command.Parameters.AddWithValue("$sapCounter", (object?)timeBlock.SapCounter ?? DBNull.Value);
    }

    private static TimeBlockRow ReadRow(SqliteDataReader reader)
    {
        var timeBlock = new TimeBlock(
            ParseDate(reader.GetString(1)),
            ParseTimestamp(reader.GetString(2)),
            ParseTimestamp(reader.GetString(3)),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetDouble(9),
            ParseStatus(reader.GetString(10)),
            reader.IsDBNull(11) ? null : reader.GetString(11));

        return new TimeBlockRow(reader.GetInt64(0), timeBlock);
    }

    private static string FormatDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly ParseDate(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);

    private static DateTime ParseTimestamp(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string FormatStatus(TimeBlockStatus status) => status switch
    {
        TimeBlockStatus.Proposed => "proposed",
        TimeBlockStatus.Edited => "edited",
        TimeBlockStatus.Validated => "validated",
        TimeBlockStatus.Submitted => "submitted",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static TimeBlockStatus ParseStatus(string value) => value switch
    {
        "proposed" => TimeBlockStatus.Proposed,
        "edited" => TimeBlockStatus.Edited,
        "validated" => TimeBlockStatus.Validated,
        "submitted" => TimeBlockStatus.Submitted,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
