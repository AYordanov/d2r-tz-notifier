using TerrorZoneNotifier;

namespace TerrorZoneNotifier.Tests;

public class TerrorZoneScheduleServiceTests
{
    // Fixed "now": 2026-02-15 09:00 UTC (the 9am cron). Tests run in UTC so the local date is
    // unambiguous, and the standing test windows (10:00+) sit after "now" so they aren't filtered.
    private static readonly DateTimeOffset NowUtc = new(2026, 2, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static TerrorZoneEntry Slot(string isoUtc, string name) => new()
    {
        DateTime = DateTimeOffset.Parse(isoUtc),
        Zone = new Dictionary<string, string> { ["enUS"] = name },
    };

    // Builds a target list from keywords; boss label = keyword (the existing tests don't assert on it).
    private static IReadOnlyList<ZoneTarget> Targets(params string[] keywords) =>
        keywords.Select(k => new ZoneTarget(k, k)).ToList();

    [Fact]
    public void Merges_back_to_back_slots_into_a_single_window()
    {
        var entries = new[]
        {
            Slot("2026-02-15T10:00:00+00:00", "Durance of Hate"),
            Slot("2026-02-15T10:30:00+00:00", "Durance of Hate"),
        };

        var result = TerrorZoneScheduleService.BuildForToday(entries, Utc, Targets("Durance"),NowUtc);

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

        var result = TerrorZoneScheduleService.BuildForToday(entries, Utc, Targets("Durance"),NowUtc);

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

        var result = TerrorZoneScheduleService.BuildForToday(entries, Utc, Targets("Durance"),NowUtc);

        var window = Assert.Single(result.Windows);
        Assert.Equal("Durance of Hate", window.Source.EnglishName);
    }

    [Fact]
    public void Keyword_match_is_case_insensitive()
    {
        var entries = new[] { Slot("2026-02-15T10:00:00+00:00", "Durance of Hate") };

        var result = TerrorZoneScheduleService.BuildForToday(entries, Utc, Targets("durance"),NowUtc);

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

        var result = TerrorZoneScheduleService.BuildForToday(entries, Utc, Targets("Durance"),NowUtc);

        // No slot covers the 15th, so today is absent from the feed and nothing matches.
        Assert.False(result.TodayPresentInFeed);
        Assert.Empty(result.Windows);
    }

    [Fact]
    public void TodayPresentInFeed_is_false_when_no_slot_covers_today()
    {
        var entries = new[] { Slot("2026-02-20T10:00:00+00:00", "Durance of Hate") };

        var result = TerrorZoneScheduleService.BuildForToday(entries, Utc, Targets("Durance"),NowUtc);

        Assert.False(result.TodayPresentInFeed);
        Assert.Empty(result.Windows);
    }

    [Fact]
    public void TodayPresentInFeed_is_true_when_today_has_any_slot_even_if_none_match()
    {
        var entries = new[] { Slot("2026-02-15T10:00:00+00:00", "Blood Moor") };

        var result = TerrorZoneScheduleService.BuildForToday(entries, Utc, Targets("Durance"),NowUtc);

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

        var result = TerrorZoneScheduleService.BuildForToday(entries, ny, Targets("Durance"),nowUtc);

        Assert.Equal(new DateOnly(2026, 2, 15), result.LocalDate);
        Assert.False(result.TodayPresentInFeed);
        Assert.Empty(result.Windows);
    }

    [Fact]
    public void Converts_window_times_into_the_local_zone()
    {
        // 10:00 UTC is 05:00 in New York (UTC-5 in February). "now" is earlier so the window stays.
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var nowUtc = new DateTimeOffset(2026, 2, 15, 8, 0, 0, TimeSpan.Zero);
        var entries = new[] { Slot("2026-02-15T10:00:00+00:00", "Durance of Hate") };

        var result = TerrorZoneScheduleService.BuildForToday(entries, ny, Targets("Durance"),nowUtc);

        var window = Assert.Single(result.Windows);
        Assert.Equal(5, window.Start.Hour);
        Assert.Equal(TimeSpan.FromHours(-5), window.Start.Offset);
    }

    [Fact]
    public void Matches_multiple_targets_and_labels_each_with_its_boss()
    {
        var targets = new[] { new ZoneTarget("Durance", "Mephisto"), new ZoneTarget("Catacombs", "Andariel") };
        var entries = new[]
        {
            Slot("2026-02-15T10:00:00+00:00", "Durance of Hate"),
            Slot("2026-02-15T14:00:00+00:00", "The Catacombs"),
        };

        var result = TerrorZoneScheduleService.BuildForToday(entries, Utc, targets, NowUtc);

        Assert.Equal(2, result.Windows.Count);
        Assert.Equal("Mephisto", result.Windows[0].Boss);
        Assert.Equal("Andariel", result.Windows[1].Boss);
    }

    [Fact]
    public void Does_not_merge_adjacent_slots_of_different_zones()
    {
        var targets = new[] { new ZoneTarget("Durance", "Mephisto"), new ZoneTarget("Catacombs", "Andariel") };
        var entries = new[]
        {
            Slot("2026-02-15T10:00:00+00:00", "Durance of Hate"),
            Slot("2026-02-15T10:30:00+00:00", "The Catacombs"), // back-to-back but a different zone
        };

        var result = TerrorZoneScheduleService.BuildForToday(entries, Utc, targets, NowUtc);

        Assert.Equal(2, result.Windows.Count);
        Assert.Equal(TimeSpan.FromMinutes(30), result.Windows[0].End - result.Windows[0].Start);
        Assert.Equal("Mephisto", result.Windows[0].Boss);
        Assert.Equal("Andariel", result.Windows[1].Boss);
    }

    [Fact]
    public void Default_window_carries_the_configured_boss_label()
    {
        var entries = new[] { Slot("2026-02-15T10:00:00+00:00", "Durance of Hate") };

        var result = TerrorZoneScheduleService.BuildForToday(entries, Utc, ZoneTarget.Default, NowUtc);

        Assert.Equal("Mephisto", Assert.Single(result.Windows).Boss);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ZoneTarget_Parse_falls_back_to_default_when_blank(string? json)
    {
        Assert.Equal(ZoneTarget.Default, ZoneTarget.Parse(json));
    }

    [Fact]
    public void ZoneTarget_Parse_reads_keyword_and_boss_from_json()
    {
        var json = "[{\"keyword\":\"Durance\",\"boss\":\"Mephisto\"},{\"keyword\":\"Catacombs\",\"boss\":\"Andariel\"}]";

        var targets = ZoneTarget.Parse(json);

        Assert.Collection(targets,
            t => { Assert.Equal("Durance", t.Keyword); Assert.Equal("Mephisto", t.Boss); },
            t => { Assert.Equal("Catacombs", t.Keyword); Assert.Equal("Andariel", t.Boss); });
    }

    [Fact]
    public void Drops_windows_that_already_ended_before_now()
    {
        // Cron runs at 09:00; an overnight zone that's already over must not be reported.
        var nowUtc = new DateTimeOffset(2026, 2, 15, 9, 0, 0, TimeSpan.Zero);
        var entries = new[]
        {
            Slot("2026-02-15T02:00:00+00:00", "Durance of Hate"), // 02:00–02:30, over by 9am
            Slot("2026-02-15T10:00:00+00:00", "Durance of Hate"), // 10:00–10:30, still ahead
        };

        var result = TerrorZoneScheduleService.BuildForToday(entries, Utc, Targets("Durance"), nowUtc);

        var window = Assert.Single(result.Windows);
        Assert.Equal(new DateTimeOffset(2026, 2, 15, 10, 0, 0, TimeSpan.Zero), window.Start);
    }

    [Fact]
    public void Keeps_a_window_that_is_still_active_at_now()
    {
        // 08:30–09:30 straddles the 09:00 run — still worth reporting.
        var nowUtc = new DateTimeOffset(2026, 2, 15, 9, 0, 0, TimeSpan.Zero);
        var entries = new[]
        {
            Slot("2026-02-15T08:30:00+00:00", "Durance of Hate"),
            Slot("2026-02-15T09:00:00+00:00", "Durance of Hate"),
        };

        var result = TerrorZoneScheduleService.BuildForToday(entries, Utc, Targets("Durance"), nowUtc);

        var window = Assert.Single(result.Windows);
        Assert.Equal(new DateTimeOffset(2026, 2, 15, 8, 30, 0, TimeSpan.Zero), window.Start);
        Assert.Equal(new DateTimeOffset(2026, 2, 15, 9, 30, 0, TimeSpan.Zero), window.End);
    }

    [Fact]
    public void LoadFromFile_returns_default_when_file_missing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");
        Assert.Equal(ZoneTarget.Default, ZoneTarget.LoadFromFile(path));
    }

    [Fact]
    public void LoadFromFile_reads_targets_from_disk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"targets-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "[{\"keyword\":\"Catacombs\",\"boss\":\"Andariel\"}]");
        try
        {
            var t = Assert.Single(ZoneTarget.LoadFromFile(path));
            Assert.Equal("Catacombs", t.Keyword);
            Assert.Equal("Andariel", t.Boss);
        }
        finally { File.Delete(path); }
    }
}
