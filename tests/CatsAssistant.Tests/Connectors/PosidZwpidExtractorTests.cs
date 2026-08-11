using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.Connectors;

public class PosidZwpidExtractorTests
{
    [Fact]
    public void Extract_RealExample_ReturnsPosidAndZwpid()
    {
        var result = PosidZwpidExtractor.Extract("ULIS (hors clients) Dev. Maint. U3 (P.ACSICAT01-01-P-0005 ZS042)");

        Assert.True(result.IsExtracted);
        Assert.Equal("P.ACSICAT01-01-P-0005", result.Posid);
        Assert.Equal("ZS042", result.Zwpid);
    }

    [Fact]
    public void Extract_HorsClientsTrap_ReturnsLastParenthesizedGroupNotFirst()
    {
        var result = PosidZwpidExtractor.Extract("ULIS (hors clients) Dev. Maint. U3 (P.ACSICAT01-01-P-0005 ZS042)");

        Assert.True(result.IsExtracted);
        Assert.NotEqual("hors", result.Posid);
        Assert.Equal("P.ACSICAT01-01-P-0005", result.Posid);
    }

    [Fact]
    public void Extract_MultipleValidLookingParenthesizedGroups_ReturnsLastGroupOnly()
    {
        var result = PosidZwpidExtractor.Extract("(P.OLD01-01 ZS000) transitional label (P.ACSICAT01-01-P-0005 ZS042)");

        Assert.True(result.IsExtracted);
        Assert.Equal("P.ACSICAT01-01-P-0005", result.Posid);
        Assert.Equal("ZS042", result.Zwpid);
    }

    [Fact]
    public void Extract_NoParenthesizedGroup_ReturnsNotExtracted()
    {
        var result = PosidZwpidExtractor.Extract("ULIS Dev. Maint. U3");

        Assert.False(result.IsExtracted);
        Assert.Null(result.Posid);
        Assert.Null(result.Zwpid);
    }

    [Fact]
    public void Extract_MalformedGroupSingleToken_ReturnsNotExtracted()
    {
        var result = PosidZwpidExtractor.Extract("ULIS Dev. Maint. U3 (P.ACSICAT01-01-P-0005)");

        Assert.False(result.IsExtracted);
    }

    [Fact]
    public void Extract_MalformedGroupLowercase_ReturnsNotExtracted()
    {
        var result = PosidZwpidExtractor.Extract("ULIS Dev. Maint. U3 (posid zwpid)");

        Assert.False(result.IsExtracted);
    }

    [Fact]
    public void Extract_TrailingWhitespace_StillExtracts()
    {
        var result = PosidZwpidExtractor.Extract("ULIS Dev. Maint. U3 (P.ACSICAT01-01-P-0005 ZS042)   ");

        Assert.True(result.IsExtracted);
        Assert.Equal("P.ACSICAT01-01-P-0005", result.Posid);
        Assert.Equal("ZS042", result.Zwpid);
    }

    [Fact]
    public void Extract_TrailingParasiteCharacters_ReturnsNotExtracted()
    {
        var result = PosidZwpidExtractor.Extract("ULIS Dev. Maint. U3 (P.ACSICAT01-01-P-0005 ZS042) note");

        Assert.False(result.IsExtracted);
    }

    [Fact]
    public void Extract_NullValue_ReturnsNotExtracted()
    {
        var result = PosidZwpidExtractor.Extract(null);

        Assert.False(result.IsExtracted);
    }

    [Fact]
    public void Extract_EmptyValue_ReturnsNotExtracted()
    {
        var result = PosidZwpidExtractor.Extract(string.Empty);

        Assert.False(result.IsExtracted);
    }
}
