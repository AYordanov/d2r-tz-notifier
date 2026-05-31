using MephistoTzNotifier;

namespace MephistoTzNotifier.Tests;

public class MephistoScheduleServiceTests
{
    // Fixed "now": 2026-02-15 12:00 UTC. Tests run in UTC so the local date is unambiguous.
    private static readonly DateTimeOffset NowUtc = new(2026, 2, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static TerrorZoneEntry Slot(string isoUtc, string name) => new()
    {
        DateTime = DateTimeOffset.Parse(isoUtc),
        Zone = new Dictionary<string, string> { ["enUS"] = name },
    };

    [Fact]
    public void Merges_back_to_back_slots_into_a_single_window()
    {
        var entries = new[]
        {
            Slot("2026-02-15T10:00:00+00:00", "Durance of Hate"),
            Slot("2026-02-15T10:30:00+00:00", "Durance of Hate"),
        };

        var result = MephistoScheduleService.BuildForToday(entries, Utc, "Durance", NowUtc);

        var window = Assert.Single(result.Windows);
        Assert.Equal(new DateTimeOffset(2026, 2, 15, 10, 0, 0, TimeSpan.Zero), window.Start);
        Assert.Equal(new DateTimeOffset(2026, 2, 15, 11, 0, 0, TimeSpan.Zero), window.End);
        Assert.Equal("Durance of Hate", window.Source.EnglishName);
    }

    [Fact]
    public void Keeps_non_contiguous_slots_as_separate_windows()
    {
        var entries = new[]
        {
            Slot("2026-02-15T10:00:00+00:00", "Durance of Hate"),
            Slot("2026-02-15T14:00:00+00:00", "Durance of Hate"),
        };

        var result = MephistoScheduleService.BuildForToday(entries, Utc, "Durance", NowUtc);

        Assert.Equal(2, result.Windows.Count);
        Assert.Equal(TimeSpan.FromMinutes(30), result.Windows[0].End - result.Windows[0].Start);
        Assert.Equal(new DateTimeOffset(2026, 2, 15, 14, 0, 0, TimeSpan.Zero), result.Windows[1].Start);
    }

    [Fact]
    public void Filters_out_zones_not_matching_the_keyword()
    {
        var entries = new[]
        {
            Slot("2026-02-15T10:00:00+00:00", "Blood Moor"),
            Slot("2026-02-15T10:30:00+00:00", "Durance of Hate"),
        };

        var result = MephistoScheduleService.BuildForToday(entries, Utc, "Durance", NowUtc);

        var window = Assert.Single(result.Windows);
        Assert.Equal("Durance of Hate", window.Source.EnglishName);
    }

    [Fact]
    public void Keyword_match_is_case_insensitive()
    {
        var entries = new[] { Slot("2026-02-15T10:00:00+00:00", "Durance of Hate") };

        var result = MephistoScheduleService.BuildForToday(entries, Utc, "durance", NowUtc);

        Assert.Single(result.Windows);
    }

    [Fact]
    public void Ignores_slots_from_other_days()
    {
        var entries = new[]
        {
            Slot("2026-02-14T10:00:00+00:00", "Durance of Hate"), // yesterday
            Slot("2026-02-16T10:00:00+00:00", "Durance of Hate"), // tomorrow
        };

        var result = MephistoScheduleService.BuildForToday(entries, Utc, "Durance", NowUtc);

        // No slot covers the 15th, so today is absent from the feed and nothing matches.
        Assert.False(result.TodayPresentInFeed);
        Assert.Empty(result.Windows);
    }

    [Fact]
    public void TodayPresentInFeed_is_false_when_no_slot_covers_today()
    {
        var entries = new[] { Slot("2026-02-20T10:00:00+00:00", "Durance of Hate") };

        var result = MephistoScheduleService.BuildForToday(entries, Utc, "Durance", NowUtc);

        Assert.False(result.TodayPresentInFeed);
        Assert.Empty(result.Windows);
    }

    [Fact]
    public void TodayPresentInFeed_is_true_when_today_has_any_slot_even_if_none_match()
    {
        var entries = new[] { Slot("2026-02-15T10:00:00+00:00", "Blood Moor") };

        var result = MephistoScheduleService.BuildForToday(entries, Utc, "Durance", NowUtc);

        Assert.True(result.TodayPresentInFeed);
        Assert.Empty(result.Windows);
    }

    [Fact]
    public void LocalDate_reflects_the_configured_zone_not_utc()
    {
        var nowUtc = new DateTimeOffset(2026, 2, 15, 23, 30, 0, TimeSpan.Zero);
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        // 23:30 UTC on the 15th is 18:30 on the 15th in New York, so "today" locally is the 15th.
        // This slot lands on the 16th locally and must be excluded.
        var entries = new[] { Slot("2026-02-16T10:00:00+00:00", "Durance of Hate") };

        var result = MephistoScheduleService.BuildForToday(entries, ny, "Durance", nowUtc);

        Assert.Equal(new DateOnly(2026, 2, 15), result.LocalDate);
        Assert.False(result.TodayPresentInFeed);
        Assert.Empty(result.Windows);
    }

    [Fact]
    public void Converts_window_times_into_the_local_zone()
    {
        // 10:00 UTC is 05:00 in New York (UTC-5 in February).
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var nowUtc = new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero);
        var entries = new[] { Slot("2026-02-15T10:00:00+00:00", "Durance of Hate") };

        var result = MephistoScheduleService.BuildForToday(entries, ny, "Durance", nowUtc);

        var window = Assert.Single(result.Windows);
        Assert.Equal(5, window.Start.Hour);
        Assert.Equal(TimeSpan.FromHours(-5), window.Start.Offset);
    }
}
