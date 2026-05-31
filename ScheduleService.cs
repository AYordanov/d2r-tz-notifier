using System.Text.Json;

namespace TerrorZoneNotifier;

/// <summary>A zone we want to be notified about, with a friendly label for the email.</summary>
/// <param name="Keyword">Case-insensitive substring matched against the feed's English zone name.</param>
/// <param name="Boss">Display label (typically the boss farmed there), shown in the email.</param>
public sealed record ZoneTarget(string Keyword, string Boss)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Used when the ZONE_TARGETS setting is unset or empty.</summary>
    public static readonly IReadOnlyList<ZoneTarget> Default = new[] { new ZoneTarget("Durance", "Mephisto") };

    /// <summary>
    /// Parses the ZONE_TARGETS app setting — a JSON array such as
    /// <c>[{"keyword":"Durance","boss":"Mephisto"},{"keyword":"Catacombs","boss":"Andariel"}]</c>.
    /// Returns <see cref="Default"/> when unset/empty; lets malformed JSON throw so the misconfig is visible in logs.
    /// </summary>
    public static IReadOnlyList<ZoneTarget> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Default;

        var parsed = JsonSerializer.Deserialize<List<ZoneTarget>>(json, JsonOpts);
        return parsed is { Count: > 0 } ? parsed : Default;
    }
}

/// <summary>A contiguous run of terror-zone slots for one zone, merged into a single local-time window.</summary>
/// <param name="Start">Window start, in the configured local zone.</param>
/// <param name="End">Window end (last slot start + <see cref="TerrorZoneScheduleService.SlotLength"/>), local.</param>
/// <param name="Source">The first slot in the window; carries zone/immunity/pack details.</param>
/// <param name="Boss">The configured boss label for the matched zone (e.g. "Mephisto", "Andariel").</param>
public sealed record TerrorZoneWindow(DateTimeOffset Start, DateTimeOffset End, TerrorZoneEntry Source, string Boss);

/// <summary>The outcome of evaluating the feed for a single local day.</summary>
/// <param name="LocalDate">The local date that was evaluated.</param>
/// <param name="TodayPresentInFeed">
/// True if the feed contained any slot for <paramref name="LocalDate"/>. When false the feed is
/// stale or its horizon has run out, and a gap alert is sent instead of a "nothing today" result.
/// </param>
/// <param name="Windows">The merged matching windows for the day, time-ordered (empty if none match).</param>
public sealed record DayResult(DateOnly LocalDate, bool TodayPresentInFeed, IReadOnlyList<TerrorZoneWindow> Windows);

/// <summary>
/// Turns the flat d2emu slot feed into the windows that match any configured <see cref="ZoneTarget"/> today.
/// Pure/deterministic: takes "now" as an argument so it can be unit-tested.
/// </summary>
public static class TerrorZoneScheduleService
{
    /// <summary>Slot cadence. 30 min under Reign of the Warlock; set to 60 for vanilla.</summary>
    public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(30);

    public static DayResult BuildForToday(
        IReadOnlyList<TerrorZoneEntry> entries,
        TimeZoneInfo localZone,
        IReadOnlyList<ZoneTarget> targets,
        DateTimeOffset nowUtc)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, localZone).DateTime);

        // Project every slot into the local zone once, then work in local time.
        var local = entries
            .Select(e => (Entry: e, Start: TimeZoneInfo.ConvertTime(e.DateTime, localZone)))
            .ToList();

        var todayPresent = local.Any(x => DateOnly.FromDateTime(x.Start.DateTime) == today);

        var matches = local
            .Where(x => DateOnly.FromDateTime(x.Start.DateTime) == today)
            .Select(x => (x.Entry, x.Start, Target: Match(x.Entry, targets)))
            .Where(x => x.Target is not null)
            .OrderBy(x => x.Start)
            .Select(x => (x.Entry, x.Start, Target: x.Target!))
            .ToList();

        return new DayResult(today, todayPresent, MergeSlots(matches));
    }

    /// <summary>First target whose keyword appears in the zone's English name, or null.</summary>
    private static ZoneTarget? Match(TerrorZoneEntry entry, IReadOnlyList<ZoneTarget> targets) =>
        targets.FirstOrDefault(t => entry.EnglishName.Contains(t.Keyword, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Coalesces back-to-back slots into single windows — but only when they're the same zone, so two
    /// different bosses that happen to be adjacent stay as separate windows.
    /// </summary>
    private static List<TerrorZoneWindow> MergeSlots(List<(TerrorZoneEntry Entry, DateTimeOffset Start, ZoneTarget Target)> slots)
    {
        var windows = new List<TerrorZoneWindow>();
        foreach (var slot in slots)
        {
            if (windows.Count > 0
                && windows[^1].End == slot.Start
                && windows[^1].Source.EnglishName == slot.Entry.EnglishName)
            {
                windows[^1] = windows[^1] with { End = slot.Start + SlotLength };
            }
            else
            {
                windows.Add(new TerrorZoneWindow(slot.Start, slot.Start + SlotLength, slot.Entry, slot.Target.Boss));
            }
        }
        return windows;
    }
}
