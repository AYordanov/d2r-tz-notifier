using System.Text;

namespace TerrorZoneNotifier;

public sealed record EmailContent(string Subject, string PlainBody, string HtmlBody);

public static class EmailContentBuilder
{
    private static readonly Dictionary<string, string> ImmunityNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c"] = "Cold", ["f"] = "Fire", ["l"] = "Lightning", ["p"] = "Poison", ["ph"] = "Physical", ["m"] = "Magic",
        ["cold"] = "Cold", ["fire"] = "Fire", ["lightning"] = "Lightning",
        ["poison"] = "Poison", ["physical"] = "Physical", ["magic"] = "Magic",
    };

    /// <summary>The main email: every matched boss/zone window today, grouped by boss.</summary>
    public static EmailContent ForWindows(DayResult result, TimeZoneInfo zone)
    {
        var dateLabel = result.LocalDate.ToString("dddd d MMM");
        var windows = result.Windows; // time-ordered

        var subject = $"Terror zones today \u2014 {string.Join(", ", windows.Select(w => $"{w.Boss} {w.Start:HH:mm}"))}";

        // Group by boss, preserving first-appearance (time) order.
        var groups = windows
            .GroupBy(w => w.Boss)
            .Select(g => (Boss: g.Key, Windows: g.ToList(), Source: g.First().Source))
            .ToList();

        var plain = new StringBuilder();
        plain.AppendLine($"Terror zones today, {dateLabel}:");
        plain.AppendLine();
        foreach (var g in groups)
        {
            plain.AppendLine($"{g.Boss} \u2014 {g.Source.EnglishName}");
            foreach (var w in g.Windows)
                plain.AppendLine($"  - {w.Start:HH:mm}\u2013{w.End:HH:mm} {ZoneLabel(zone, w.Start)}");

            var immunities = Readable(g.Source.Immunities);
            if (immunities.Count > 0) plain.AppendLine($"  Immunities: {string.Join(", ", immunities)}");
            plain.AppendLine($"  Boss packs: {Packs(g.Source)}");
            plain.AppendLine($"  Super uniques: {Uniques(g.Source)}");
            plain.AppendLine();
        }
        plain.AppendLine("All times local. Source: d2emu.com single-player calendar.");

        var html = new StringBuilder();
        html.Append("<div style='font-family:system-ui,Segoe UI,Arial,sans-serif;max-width:560px'>");
        html.Append("<h2 style='margin:0 0 4px'>Terror zones today</h2>");
        html.Append($"<p style='color:#666;margin:0 0 16px'>{dateLabel}</p>");
        foreach (var g in groups)
        {
            html.Append($"<h3 style='margin:16px 0 4px'>{WebEncode(g.Boss)} <span style='color:#999;font-weight:400'>&middot; {WebEncode(g.Source.EnglishName)}</span></h3>");
            html.Append("<table style='border-collapse:collapse;width:100%;margin-bottom:8px'>");
            foreach (var w in g.Windows)
            {
                html.Append($"<tr><td style='padding:8px 12px;background:#f4f1ea;border-radius:6px;font-size:18px;font-weight:600'>{w.Start:HH:mm}\u2013{w.End:HH:mm} {ZoneLabel(zone, w.Start)}</td></tr>");
                html.Append("<tr><td style='height:6px'></td></tr>");
            }
            html.Append("</table>");
            html.Append("<ul style='color:#444;line-height:1.6;padding-left:18px;margin:0 0 8px'>");
            var immunities = Readable(g.Source.Immunities);
            if (immunities.Count > 0) html.Append($"<li>Immunities: {string.Join(", ", immunities)}</li>");
            html.Append($"<li>Boss packs: {Packs(g.Source)}</li>");
            html.Append($"<li>Super uniques: {WebEncode(Uniques(g.Source))}</li>");
            html.Append("</ul>");
        }
        html.Append("<p style='color:#999;font-size:12px'>All times local. Source: d2emu.com single-player calendar.</p>");
        html.Append("</div>");

        return new EmailContent(subject, plain.ToString(), html.ToString());
    }

    /// <summary>Sent only when SEND_WHEN_NONE is true and none of the configured zones are up today.</summary>
    public static EmailContent NoneToday(DayResult result)
    {
        var dateLabel = result.LocalDate.ToString("dddd d MMM");
        const string subject = "No tracked terror zones today";
        var plain = $"None of your tracked terror zones are on the schedule today ({dateLabel}).";
        var html = $"<div style='font-family:system-ui,Segoe UI,Arial,sans-serif'><p>{WebEncode(plain)}</p></div>";
        return new EmailContent(subject, plain, html);
    }

    /// <summary>Always sent when the feed has no slots for today (stale file / horizon exhausted).</summary>
    public static EmailContent FeedGapAlert(DayResult result)
    {
        const string subject = "Terror zone notifier: today missing from feed";
        var plain =
            $"The d2emu feed contained no slots for today ({result.LocalDate:yyyy-MM-dd}). " +
            "The file may be stale or its forward horizon may have run out. " +
            "Check https://d2emu.com/tz-sp and the FEED_URL setting.";
        var html = $"<div style='font-family:system-ui,Segoe UI,Arial,sans-serif'><p>{plain}</p></div>";
        return new EmailContent(subject, plain, html);
    }

    private static string Packs(TerrorZoneEntry s) =>
        s.NumBossPacks.Count == 2
            ? $"{s.NumBossPacks[0]}–{s.NumBossPacks[1]}"
            : string.Join(", ", s.NumBossPacks);

    private static string Uniques(TerrorZoneEntry s) =>
        s.SuperUniques.Count > 0 ? string.Join(", ", s.SuperUniques) : "—";

    private static List<string> Readable(IEnumerable<string> codes) =>
        codes.Select(c => ImmunityNames.TryGetValue(c, out var n) ? n : c)
             .Distinct()
             .ToList();

    // BST/GMT for the UK zone; explicit UTC offset for any other configured zone.
    private static string ZoneLabel(TimeZoneInfo tz, DateTimeOffset at) =>
        tz.Id is "Europe/London" or "GMT Standard Time"
            ? (tz.IsDaylightSavingTime(at) ? "BST" : "GMT")
            : $"UTC{at:zzz}";

    private static string WebEncode(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
