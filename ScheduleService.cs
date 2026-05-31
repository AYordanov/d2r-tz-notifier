namespace MephistoTzNotifier;

/// <summary>A contiguous run of terror-zone slots merged into one local-time window.</summary>
/// <param name="Start">Window start, in the configured local zone.</param>
/// <param name="End">Window end (last slot start + <see cref="MephistoScheduleService.SlotLength"/>), local.</param>
/// <param name="Source">The first slot in the window; carries zone/immunity/pack details.</param>
public sealed record MephistoWindow(DateTimeOffset Start, DateTimeOffset End, TerrorZoneEntry Source);

/// <summary>The outcome of evaluating the feed for a single local day.</summary>
/// <param name="LocalDate">The local date that was evaluated.</param>
/// <param name="TodayPresentInFeed">
/// True if the feed contained any slot for <paramref name="LocalDate"/>. When false the feed is
/// stale or its horizon has run out, and a gap alert is sent instead of a "nothing today" result.
/// </param>
/// <param name="Windows">The merged matching windows for the day (empty if none match).</param>
public sealed record DayResult(DateOnly LocalDate, bool TodayPresentInFeed, IReadOnlyList<MephistoWindow> Windows);

/// <summary>
/// Turns the flat d2emu slot feed into the windows that match the configured zone keyword today.
/// Pure/deterministic: takes "now" as an argument so it can be unit-tested.
/// </summary>
public static class MephistoScheduleService
{
    /// <summary>Slot cadence. 30 min under Reign of the Warlock; set to 60 for vanilla.</summary>
    public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(30);

    public static DayResult BuildForToday(
        IReadOnlyList<TerrorZoneEntry> entries,
        TimeZoneInfo localZone,
        string zoneKeyword,
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
            .Where(x => x.Entry.EnglishName.Contains(zoneKeyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Start)
            .ToList();

        return new DayResult(today, todayPresent, MergeSlots(matches));
    }

    /// <summary>Coalesces back-to-back slots (each <see cref="SlotLength"/> apart) into single windows.</summary>
    private static List<MephistoWindow> MergeSlots(List<(TerrorZoneEntry Entry, DateTimeOffset Start)> slots)
    {
        var windows = new List<MephistoWindow>();
        foreach (var slot in slots)
        {
            if (windows.Count > 0 && windows[^1].End == slot.Start)
                windows[^1] = windows[^1] with { End = slot.Start + SlotLength };
            else
                windows.Add(new MephistoWindow(slot.Start, slot.Start + SlotLength, slot.Entry));
        }
        return windows;
    }
}
