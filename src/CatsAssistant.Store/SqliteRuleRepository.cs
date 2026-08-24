using Microsoft.Data.Sqlite;

namespace CatsAssistant.Store;

public sealed class SqliteRuleRepository : IRuleRepository
{
    private readonly SqliteConnection _connection;
    private readonly object _gate = new();

    public SqliteRuleRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public long Insert(Rule rule)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO rules (matcher_kind, matcher_value, target, priority, origin)
                VALUES ($matcherKind, $matcherValue, $target, $priority, $origin)
                RETURNING id;
                """;
            BindParameters(command, rule);
            return (long)command.ExecuteScalar()!;
        }
    }

    public void Update(long id, Rule rule)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                UPDATE rules
                SET matcher_kind = $matcherKind, matcher_value = $matcherValue, target = $target,
                    priority = $priority, origin = $origin
                WHERE id = $id;
                """;
            BindParameters(command, rule);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }

    public void Delete(long id)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM rules WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<RuleRow> GetAll()
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT id, matcher_kind, matcher_value, target, priority, origin
                FROM rules
                ORDER BY priority, id;
                """;

            var results = new List<RuleRow>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(ReadRow(reader));
            }

            return results;
        }
    }

    private static void BindParameters(SqliteCommand command, Rule rule)
    {
        command.Parameters.AddWithValue("$matcherKind", FormatMatcherKind(rule.MatcherKind));
        command.Parameters.AddWithValue("$matcherValue", rule.MatcherValue);
        command.Parameters.AddWithValue("$target", rule.Target);
        command.Parameters.AddWithValue("$priority", rule.Priority);
        command.Parameters.AddWithValue("$origin", FormatOrigin(rule.Origin));
    }

    private static RuleRow ReadRow(SqliteDataReader reader)
    {
        var rule = new Rule(
            ParseMatcherKind(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            ParseOrigin(reader.GetString(5)));

        return new RuleRow(reader.GetInt64(0), rule);
    }

    private static string FormatMatcherKind(RuleMatcherKind kind) => kind switch
    {
        RuleMatcherKind.Process => "process",
        RuleMatcherKind.TitleRegex => "title_regex",
        RuleMatcherKind.UrlRegex => "url_regex",
        RuleMatcherKind.JiraProject => "jira_project",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static RuleMatcherKind ParseMatcherKind(string value) => value switch
    {
        "process" => RuleMatcherKind.Process,
        "title_regex" => RuleMatcherKind.TitleRegex,
        "url_regex" => RuleMatcherKind.UrlRegex,
        "jira_project" => RuleMatcherKind.JiraProject,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static string FormatOrigin(RuleOrigin origin) => origin switch
    {
        RuleOrigin.Manual => "manual",
        RuleOrigin.Learned => "learned",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
    };

    private static RuleOrigin ParseOrigin(string value) => value switch
    {
        "manual" => RuleOrigin.Manual,
        "learned" => RuleOrigin.Learned,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
