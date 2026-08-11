using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.Connectors;

public class AdfDocumentParserTests
{
    [Fact]
    public void ToPlainText_SimpleDocument_ReturnsConcatenatedText()
    {
        const string adf = """
            {
              "type": "doc",
              "version": 1,
              "content": [
                {
                  "type": "paragraph",
                  "content": [
                    { "type": "text", "text": "Ticket synthetique de test" }
                  ]
                }
              ]
            }
            """;

        var text = AdfDocumentParser.ToPlainText(adf);

        Assert.Equal("Ticket synthetique de test", text);
    }

    [Fact]
    public void ToPlainText_NestedDocument_ReturnsTextAcrossNestedNodes()
    {
        const string adf = """
            {
              "type": "doc",
              "version": 1,
              "content": [
                {
                  "type": "bulletList",
                  "content": [
                    {
                      "type": "listItem",
                      "content": [
                        {
                          "type": "paragraph",
                          "content": [ { "type": "text", "text": "Premier point" } ]
                        }
                      ]
                    },
                    {
                      "type": "listItem",
                      "content": [
                        {
                          "type": "paragraph",
                          "content": [ { "type": "text", "text": "Second point" } ]
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var text = AdfDocumentParser.ToPlainText(adf);

        Assert.Contains("Premier point", text);
        Assert.Contains("Second point", text);
        Assert.True(text.IndexOf("Premier point", StringComparison.Ordinal) < text.IndexOf("Second point", StringComparison.Ordinal));
    }

    [Fact]
    public void ToPlainText_EmptyDocument_ReturnsEmptyString()
    {
        const string adf = """{ "type": "doc", "version": 1, "content": [] }""";

        var text = AdfDocumentParser.ToPlainText(adf);

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void ToPlainText_InvalidJson_ReturnsEmptyStringWithoutThrowing()
    {
        var text = AdfDocumentParser.ToPlainText("{ not valid json");

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void ToPlainText_UnknownNodeType_DegradesGracefullyWithoutThrowing()
    {
        const string adf = """
            {
              "type": "doc",
              "version": 1,
              "content": [
                {
                  "type": "mediaSingle",
                  "content": [
                    { "type": "media", "attrs": { "id": "synthetic-id" } }
                  ]
                },
                {
                  "type": "paragraph",
                  "content": [ { "type": "text", "text": "Texte apres noeud inconnu" } ]
                }
              ]
            }
            """;

        var text = AdfDocumentParser.ToPlainText(adf);

        Assert.Contains("Texte apres noeud inconnu", text);
    }

    [Fact]
    public void ToPlainText_NullOrWhitespace_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, AdfDocumentParser.ToPlainText(null));
        Assert.Equal(string.Empty, AdfDocumentParser.ToPlainText("   "));
    }
}
