namespace TerrorZoneNotifier;

/// <summary>
/// Builds the dynamic-template data objects passed to SendGrid. All presentation (HTML, fonts,
/// styling) lives in the SendGrid dynamic template — here we only shape the data it renders.
/// Property names are camelCase to match the Handlebars variables in Templates/sendgrid-template.html.
/// </summary>
public static class EmailData
{
    private static readonly Dictionary<string, string> ImmunityNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c"] = "Cold", ["f"] = "Fire", ["l"] = "Lightning", ["p"] = "Poison", ["ph"] = "Physical", ["m"] = "Magic",
        ["cold"] = "Cold", ["fire"] = "Fire", ["lightning"] = "Lightning",
        ["poison"] = "Poison", ["physical"] = "Physical", ["magic"] = "Magic",
    };

    /// <summary>The main email: every matched boss/zone window today, grouped by boss.</summary>
    public static object ForWindows(DayResult result, TimeZoneInfo zone)
    {
        var windows = result.Windows; // time-ordered

        var groups = windows
            .GroupBy(w => w.Boss)
            .Select(g => new
            {
                boss = g.Key,
                zone = g.First().Source.EnglishName,
                windows = g.Select(w => new { time = $"{w.Start:HH:mm}–{w.End:HH:mm} {ZoneLabel(zone, w.Start)}" }).ToList(),
                immunities = string.Join(", ", Readable(g.First().Source.Immunities)),
                packs = Packs(g.First().Source),
                uniques = Uniques(g.First().Source),
            })
            .ToList();

        return new
        {
            subject = $"Terror zones today — {string.Join(", ", windows.Select(w => $"{w.Boss} {w.Start:HH:mm}"))}",
            dateLabel = result.LocalDate.ToString("dddd d MMM"),
            hasWindows = true,
            isFeedGap = false,
            groups,
        };
    }

    /// <summary>Sent only when SEND_WHEN_NONE is true and none of the configured zones are up today.</summary>
    public static object NoneToday(DayResult result) => new
    {
        subject = "No tracked terror zones today",
        dateLabel = result.LocalDate.ToString("dddd d MMM"),
        hasWindows = false,
        isFeedGap = false,
        groups = Array.Empty<object>(),
    };

    /// <summary>Always sent when the feed has no slots for today (stale file / horizon exhausted).</summary>
    public static object FeedGap(DayResult result) => new
    {
        subject = "Terror zone notifier: today missing from feed",
        dateLabel = result.LocalDate.ToString("yyyy-MM-dd"),
        hasWindows = false,
        isFeedGap = true,
        groups = Array.Empty<object>(),
    };

    private static string Packs(TerrorZoneEntry s) =>
        s.NumBossPacks.Count == 2 ? $"{s.NumBossPacks[0]}–{s.NumBossPacks[1]}" : string.Join(", ", s.NumBossPacks);

    private static string Uniques(TerrorZoneEntry s) =>
        s.SuperUniques.Count > 0 ? string.Join(", ", s.SuperUniques) : "—";

    private static List<string> Readable(IEnumerable<string> codes) =>
        codes.Select(c => ImmunityNames.TryGetValue(c, out var n) ? n : c).Distinct().ToList();

    // BST/GMT for the UK zone; explicit UTC offset for any other configured zone.
    private static string ZoneLabel(TimeZoneInfo tz, DateTimeOffset at) =>
        tz.Id is "Europe/London" or "GMT Standard Time"
            ? (tz.IsDaylightSavingTime(at) ? "BST" : "GMT")
            : $"UTC{at:zzz}";
}
