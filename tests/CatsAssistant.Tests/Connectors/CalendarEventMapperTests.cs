using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.Connectors;

public class CalendarEventMapperTests
{
    private static readonly TimeZoneInfo FixedPlusTwoHours = TimeZoneInfo.CreateCustomTimeZone(
        "Test/PlusTwo", TimeSpan.FromHours(2), "Test +02:00", "Test +02:00");

    [Fact]
    public void Map_ConvertsLocalTimesToUtcUsingProvidedTimeZone()
    {
        var start = new DateTime(2026, 8, 11, 14, 0, 0);
        var end = new DateTime(2026, 8, 11, 15, 0, 0);

        var result = CalendarEventMapper.Map(start, end, "Point projet", "Alice Dupont", FixedPlusTwoHours);

        Assert.Equal(new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc), result.StartUtc);
        Assert.Equal(new DateTime(2026, 8, 11, 13, 0, 0, DateTimeKind.Utc), result.EndUtc);
    }

    [Fact]
    public void Map_TrimsSubjectAndOrganizer()
    {
        var start = new DateTime(2026, 8, 11, 14, 0, 0);
        var end = new DateTime(2026, 8, 11, 15, 0, 0);

        var result = CalendarEventMapper.Map(start, end, "  Point projet  ", "  Alice Dupont  ", FixedPlusTwoHours);

        Assert.Equal("Point projet", result.Subject);
        Assert.Equal("Alice Dupont", result.Organizer);
    }

    [Fact]
    public void Map_BlankSubject_FallsBackToPlaceholder()
    {
        var start = new DateTime(2026, 8, 11, 14, 0, 0);
        var end = new DateTime(2026, 8, 11, 15, 0, 0);

        var result = CalendarEventMapper.Map(start, end, "   ", "Alice Dupont", FixedPlusTwoHours);

        Assert.Equal("(sans objet)", result.Subject);
    }

    [Fact]
    public void Map_NullSubject_FallsBackToPlaceholder()
    {
        var start = new DateTime(2026, 8, 11, 14, 0, 0);
        var end = new DateTime(2026, 8, 11, 15, 0, 0);

        var result = CalendarEventMapper.Map(start, end, null, "Alice Dupont", FixedPlusTwoHours);

        Assert.Equal("(sans objet)", result.Subject);
    }

    [Fact]
    public void Map_BlankOrganizer_ReturnsNull()
    {
        var start = new DateTime(2026, 8, 11, 14, 0, 0);
        var end = new DateTime(2026, 8, 11, 15, 0, 0);

        var result = CalendarEventMapper.Map(start, end, "Point projet", "   ", FixedPlusTwoHours);

        Assert.Null(result.Organizer);
    }

    [Fact]
    public void Map_NullOrganizer_ReturnsNull()
    {
        var start = new DateTime(2026, 8, 11, 14, 0, 0);
        var end = new DateTime(2026, 8, 11, 15, 0, 0);

        var result = CalendarEventMapper.Map(start, end, "Point projet", null, FixedPlusTwoHours);

        Assert.Null(result.Organizer);
    }

    [Fact]
    public void Map_EndBeforeStart_Throws()
    {
        var start = new DateTime(2026, 8, 11, 15, 0, 0);
        var end = new DateTime(2026, 8, 11, 14, 0, 0);

        Assert.Throws<ArgumentException>(() => CalendarEventMapper.Map(start, end, "Point projet", "Alice Dupont", FixedPlusTwoHours));
    }

    [Fact]
    public void Map_EqualStartAndEnd_Allowed()
    {
        var start = new DateTime(2026, 8, 11, 14, 0, 0);

        var result = CalendarEventMapper.Map(start, start, "Point projet", "Alice Dupont", FixedPlusTwoHours);

        Assert.Equal(result.StartUtc, result.EndUtc);
    }
}
