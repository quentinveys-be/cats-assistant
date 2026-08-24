using System.Globalization;
using CatsAssistant.Connectors;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Store;

public sealed class SqliteVcsCommitRepository : IVcsCommitRepository
{
    private readonly SqliteConnection _connection;
    private readonly object _gate = new();

    public SqliteVcsCommitRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public void Upsert(VcsCommit commit)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO vcs_commits (sha, ts, repo, branch, message, jira_key)
                VALUES ($sha, $ts, $repo, $branch, $message, $jiraKey)
                ON CONFLICT(sha, repo) DO UPDATE SET
                    ts = excluded.ts,
                    branch = excluded.branch,
                    message = excluded.message,
                    jira_key = excluded.jira_key;
                """;
            command.Parameters.AddWithValue("$sha", commit.Sha);
            command.Parameters.AddWithValue("$ts", FormatTimestamp(commit.TimestampUtc));
            command.Parameters.AddWithValue("$repo", commit.Repo);
            command.Parameters.AddWithValue("$branch", commit.Branch);
            command.Parameters.AddWithValue("$message", commit.Message);
            command.Parameters.AddWithValue("$jiraKey", (object?)commit.JiraKey ?? DBNull.Value);
            command.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<VcsCommit> GetByDateRange(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT sha, ts, repo, branch, message, jira_key
                FROM vcs_commits
                WHERE ts >= $from AND ts < $to
                ORDER BY ts;
                """;
            command.Parameters.AddWithValue("$from", FormatTimestamp(fromUtc));
            command.Parameters.AddWithValue("$to", FormatTimestamp(toUtc));

            var results = new List<VcsCommit>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(ReadCommit(reader));
            }

            return results;
        }
    }

    private static VcsCommit ReadCommit(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            ParseTimestamp(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.None);
}
