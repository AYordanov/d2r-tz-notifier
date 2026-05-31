using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TerrorZoneNotifier;

public sealed class NotifierFunctions
{
    private const string DefaultFeedUrl = "https://d2emu.com/data/tz-2023-localized.json";

    private readonly D2EmuClient _d2emu;
    private readonly EmailSender _email;
    private readonly IConfiguration _config;
    private readonly ILogger<NotifierFunctions> _log;

    public NotifierFunctions(D2EmuClient d2emu, EmailSender email, IConfiguration config, ILogger<NotifierFunctions> log)
    {
        _d2emu = d2emu;
        _email = email;
        _config = config;
        _log = log;
    }

    // 09:00 every day. Interpreted in the WEBSITE_TIME_ZONE app setting (see README) so it
    // tracks 9am UK local across the BST/GMT switch rather than drifting.
    [Function("TerrorZoneNotifier")]
    public Task RunScheduled([TimerTrigger("0 0 9 * * *")] TimerInfo timer, CancellationToken ct)
        => ExecuteAsync(ct);

    // Manual trigger for testing: GET/POST the function URL to run immediately.
    [Function("RunNow")]
    public async Task<HttpResponseData> RunNow(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
        CancellationToken ct)
    {
        await ExecuteAsync(ct);
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteStringAsync("Mephisto notifier ran — check the logs and your inbox.", ct);
        return resp;
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        var feedUrl = _config["FEED_URL"] ?? DefaultFeedUrl;
        var targets = ZoneTarget.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "zone-targets.json"));
        var tzId = _config["TIME_ZONE_ID"] ?? "Europe/London";
        var sendWhenNone = bool.TryParse(_config["SEND_WHEN_NONE"], out var s) && s;

        var localZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);

        var entries = await _d2emu.FetchScheduleAsync(feedUrl, ct);
        _log.LogInformation("Fetched {Count} terror-zone slots from feed.", entries.Count);

        var result = TerrorZoneScheduleService.BuildForToday(entries, localZone, targets, DateTimeOffset.UtcNow);

        if (!result.TodayPresentInFeed)
        {
            _log.LogWarning("Today ({Date}) is absent from the feed — sending gap alert.", result.LocalDate);
            await _email.SendAsync(EmailContentBuilder.FeedGapAlert(result), ct);
            return;
        }

        if (result.Windows.Count == 0)
        {
            _log.LogInformation("No tracked terror zones today.");
            if (sendWhenNone)
                await _email.SendAsync(EmailContentBuilder.NoneToday(result), ct);
            return;
        }

        _log.LogInformation("{Count} tracked window(s) today; emailing.", result.Windows.Count);
        await _email.SendAsync(EmailContentBuilder.ForWindows(result, localZone), ct);
    }
}
