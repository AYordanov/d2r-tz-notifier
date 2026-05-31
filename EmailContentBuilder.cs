using System.Text;

namespace MephistoTzNotifier;

public sealed record EmailContent(string Subject, string PlainBody, string HtmlBody);

public static class EmailContentBuilder
{
    private static readonly Dictionary<string, string> ImmunityNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c"] = "Cold", ["f"] = "Fire", ["l"] = "Lightning", ["p"] = "Poison", ["ph"] = "Physical", ["m"] = "Magic",
        ["cold"] = "Cold", ["fire"] = "Fire", ["lightning"] = "Lightning",
        ["poison"] = "Poison", ["physical"] = "Physical", ["magic"] = "Magic",
    };

    /// <summary>The main email: one or more Mephisto windows today.</summary>
    public static EmailContent ForWindows(DayResult result, TimeZoneInfo zone)
    {
        var source = result.Windows[0].Source;
        var dateLabel = result.LocalDate.ToString("dddd d MMM");

        var windowStrings = result.Windows
            .Select(w => $"{w.Start:HH:mm}\u2013{w.End:HH:mm} {ZoneLabel(zone, w.Start)}")
            .ToList();

        var subject = $"Mephisto TZ today \u2014 {string.Join(", ", result.Windows.Select(w => w.Start.ToString("HH:mm")))}";

        var immunities = Readable(source.Immunities);
        var packs = source.NumBossPacks.Count == 2
            ? $"{source.NumBossPacks[0]}\u2013{source.NumBossPacks[1]}"
            : string.Join(", ", source.NumBossPacks);
        var uniques = source.SuperUniques.Count > 0 ? string.Join(", ", source.SuperUniques) : "\u2014";

        var plain = new StringBuilder();
        plain.AppendLine($"Mephisto is terrorized {result.Windows.Count} time(s) today, {dateLabel}:");
        plain.AppendLine();
        foreach (var w in windowStrings) plain.AppendLine($"  - {w}");
        plain.AppendLine();
        plain.AppendLine($"Zone: {source.EnglishName}");
        if (immunities.Count > 0) plain.AppendLine($"Immunities: {string.Join(", ", immunities)}");
        plain.AppendLine($"Boss packs: {packs}");
        plain.AppendLine($"Super uniques: {uniques}");
        plain.AppendLine();
        plain.AppendLine("All times local. Source: d2emu.com single-player calendar.");

        var html = new StringBuilder();
        html.Append("<div style='font-family:system-ui,Segoe UI,Arial,sans-serif;max-width:560px'>");
        html.Append("<h2 style='margin:0 0 4px'>Mephisto today</h2>");
        html.Append($"<p style='color:#666;margin:0 0 16px'>{dateLabel} &middot; {WebEncode(source.EnglishName)}</p>");
        html.Append("<table style='border-collapse:collapse;width:100%;margin-bottom:16px'>");
        foreach (var w in windowStrings)
        {
            html.Append($"<tr><td style='padding:8px 12px;background:#f4f1ea;border-radius:6px;font-size:18px;font-weight:600'>{w}</td></tr>");
            html.Append("<tr><td style='height:6px'></td></tr>");
        }
        html.Append("</table>");
        html.Append("<ul style='color:#444;line-height:1.6;padding-left:18px'>");
        if (immunities.Count > 0) html.Append($"<li>Immunities: {string.Join(", ", immunities)}</li>");
        html.Append($"<li>Boss packs: {packs}</li>");
        html.Append($"<li>Super uniques: {WebEncode(uniques)}</li>");
        html.Append("</ul>");
        html.Append("<p style='color:#999;font-size:12px'>All times local. Source: d2emu.com single-player calendar.</p>");
        html.Append("</div>");

        return new EmailContent(subject, plain.ToString(), html.ToString());
    }

    /// <summary>Optional: sent only when SEND_WHEN_NONE is true and there's no Mephisto today.</summary>
    public static EmailContent NoneToday(DayResult result)
    {
        var dateLabel = result.LocalDate.ToString("dddd d MMM");
        const string subject = "No Mephisto terror zone today";
        var plain = $"Mephisto (Durance of Hate) is not on the terror-zone schedule today ({dateLabel}).";
        var html = $"<div style='font-family:system-ui,Segoe UI,Arial,sans-serif'><p>{plain}</p></div>";
        return new EmailContent(subject, plain, html);
    }

    /// <summary>Always sent when the feed has no slots for today (stale file / horizon exhausted).</summary>
    public static EmailContent FeedGapAlert(DayResult result)
    {
        const string subject = "Mephisto notifier: today missing from feed";
        var plain =
            $"The d2emu feed contained no slots for today ({result.LocalDate:yyyy-MM-dd}). " +
            "The file may be stale or its forward horizon may have run out. " +
            "Check https://d2emu.com/tz-sp and the FEED_URL setting.";
        var html = $"<div style='font-family:system-ui,Segoe UI,Arial,sans-serif'><p>{plain}</p></div>";
        return new EmailContent(subject, plain, html);
    }

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
