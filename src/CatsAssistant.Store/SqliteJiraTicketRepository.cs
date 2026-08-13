using System.Globalization;
using CatsAssistant.Connectors;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Store;

public sealed class SqliteJiraTicketRepository : IJiraTicketRepository
{
    private readonly SqliteConnection _connection;

    // Same rationale as SqliteActivityEventRepository: serialise access, a single SqliteConnection
    // does not support concurrent callers.
    private readonly object _gate = new();

    public SqliteJiraTicketRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public void Upsert(JiraTicket ticket, DateTime lastSyncUtc)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO jira_tickets ("key", summary, status, context, imputation_code_raw, posid, zwpid, effort, last_sync)
                VALUES ($key, $summary, $status, $context, $imputationCodeRaw, $posid, $zwpid, $effort, $lastSync)
                ON CONFLICT("key") DO UPDATE SET
                    summary = excluded.summary,
                    status = excluded.status,
                    context = excluded.context,
                    imputation_code_raw = excluded.imputation_code_raw,
                    posid = excluded.posid,
                    zwpid = excluded.zwpid,
                    effort = excluded.effort,
                    last_sync = excluded.last_sync;
                """;
            command.Parameters.AddWithValue("$key", ticket.Key);
            command.Parameters.AddWithValue("$summary", (object?)ticket.Summary ?? DBNull.Value);
            command.Parameters.AddWithValue("$status", (object?)ticket.Status ?? DBNull.Value);
            command.Parameters.AddWithValue("$context", (object?)ticket.Context ?? DBNull.Value);
            command.Parameters.AddWithValue("$imputationCodeRaw", (object?)ticket.ImputationCodeRaw ?? DBNull.Value);
            command.Parameters.AddWithValue("$posid", (object?)ticket.Posid ?? DBNull.Value);
            command.Parameters.AddWithValue("$zwpid", (object?)ticket.Zwpid ?? DBNull.Value);
            command.Parameters.AddWithValue("$effort", (object?)ticket.Effort ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastSync", FormatTimestamp(lastSyncUtc));
            command.ExecuteNonQuery();
        }
    }

    public JiraTicketRow? GetByKey(string key)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT "key", summary, status, context, imputation_code_raw, posid, zwpid, effort, last_sync
                FROM jira_tickets
                WHERE "key" = $key;
                """;
            command.Parameters.AddWithValue("$key", key);

            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadRow(reader) : null;
        }
    }

    public IReadOnlyList<JiraTicketRow> GetAll()
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT "key", summary, status, context, imputation_code_raw, posid, zwpid, effort, last_sync
                FROM jira_tickets
                ORDER BY "key";
                """;

            var results = new List<JiraTicketRow>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(ReadRow(reader));
            }

            return results;
        }
    }

    private static JiraTicketRow ReadRow(SqliteDataReader reader)
    {
        var ticket = new JiraTicket(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetDouble(7));

        return new JiraTicketRow(ticket, ParseTimestamp(reader.GetString(8)));
    }

    private static string FormatTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);

    private static DateTime ParseTimestamp(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
