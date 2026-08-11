using System.Text;
using System.Text.Json;

namespace CatsAssistant.Connectors;

public static class AdfDocumentParser
{
    private static readonly HashSet<string> LineBreakingTypes = new()
    {
        "paragraph", "listItem", "heading", "blockquote", "codeBlock",
    };

    public static string ToPlainText(string? adfJson)
    {
        if (string.IsNullOrWhiteSpace(adfJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(adfJson);
            var builder = new StringBuilder();
            AppendNode(document.RootElement, builder);
            return builder.ToString().Trim();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void AppendNode(JsonElement node, StringBuilder builder)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var type = node.TryGetProperty("type", out var typeProperty) && typeProperty.ValueKind == JsonValueKind.String
            ? typeProperty.GetString()
            : null;

        if (type == "text")
        {
            if (node.TryGetProperty("text", out var textProperty) && textProperty.ValueKind == JsonValueKind.String)
            {
                builder.Append(textProperty.GetString());
            }

            return;
        }

        if (node.TryGetProperty("content", out var contentProperty) && contentProperty.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in contentProperty.EnumerateArray())
            {
                AppendNode(child, builder);
            }
        }

        if (type is not null && LineBreakingTypes.Contains(type))
        {
            builder.Append('\n');
        }
    }
}
