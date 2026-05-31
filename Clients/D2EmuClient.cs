using System.Text.Json;

namespace TerrorZoneNotifier;

/// <summary>Fetches the d2emu single-player terror-zone calendar (a static JSON array).</summary>
public sealed class D2EmuClient
{
    private readonly HttpClient _http;

    public D2EmuClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<TerrorZoneEntry>> FetchScheduleAsync(string feedUrl, CancellationToken ct)
    {
        await using var stream = await _http.GetStreamAsync(feedUrl, ct);
        var entries = await JsonSerializer.DeserializeAsync<List<TerrorZoneEntry>>(stream, cancellationToken: ct);
        return entries ?? new List<TerrorZoneEntry>();
    }
}
