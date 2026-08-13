using System.Globalization;
using CatsAssistant.Connectors;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Store;

public sealed class SqliteCalendarEventRepository : ICalendarEventRepository
{
    private readonly SqliteConnection _connection;
    private readonly object _gate = new();

    public SqliteCalendarEventRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public long Insert(CalendarEventData calendarEvent)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO calendar_events ("start", "end", subject, organizer)
                VALUES ($start, $end, $subject, $organizer)
                RETURNING id;
                """;
            command.Parameters.AddWithValue("$start", FormatTimestamp(calendarEvent.StartUtc));
            command.Parameters.AddWithValue("$end", FormatTimestamp(calendarEvent.EndUtc));
            command.Parameters.AddWithValue("$subject", calendarEvent.Subject);
            command.Parameters.AddWithValue("$organizer", (object?)calendarEvent.Organizer ?? DBNull.Value);
            return (long)command.ExecuteScalar()!;
        }
    }

    public IReadOnlyList<CalendarEventData> GetByDateRange(DateTime fromUtc, DateTime toUtc)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT "start", "end", subject, organizer
                FROM calendar_events
                WHERE "start" >= $from AND "start" < $to
                ORDER BY "start";
                """;
            command.Parameters.AddWithValue("$from", FormatTimestamp(fromUtc));
            command.Parameters.AddWithValue("$to", FormatTimestamp(toUtc));

            var results = new List<CalendarEventData>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(ReadCalendarEvent(reader));
            }

            return results;
        }
    }

    private static CalendarEventData ReadCalendarEvent(SqliteDataReader reader) =>
        new(
            ParseTimestamp(reader.GetString(0)),
            ParseTimestamp(reader.GetString(1)),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));

    private static string FormatTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);

    private static DateTime ParseTimestamp(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
