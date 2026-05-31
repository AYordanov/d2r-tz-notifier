using System.Text.Json.Serialization;

namespace TerrorZoneNotifier;

/// <summary>
/// One terror-zone slot from the d2emu single-player feed.
/// The feed is a flat JSON array of these, ordered by datetime.
/// Under Reign of the Warlock the slots are 30 minutes apart.
/// </summary>
public sealed record TerrorZoneEntry
{
    /// <summary>
    /// Slot start time. The feed serialises this with an explicit offset
    /// (e.g. "2026-02-15T00:00:00+00:00"), i.e. UTC. DateTimeOffset preserves that.
    /// </summary>
    [JsonPropertyName("datetime")]
    public DateTimeOffset DateTime { get; init; }

    /// <summary>
    /// Zone name keyed by Blizzard locale (enUS, deDE, frFR, ...). We read enUS.
    /// Modelled as a dictionary so it works whether the feed carries 1 locale or 13.
    /// </summary>
    [JsonPropertyName("zone")]
    public Dictionary<string, string> Zone { get; init; } = new();

    [JsonPropertyName("immunities")]
    public List<string> Immunities { get; init; } = new();

    /// <summary>[min, max] boss-pack count for the zone.</summary>
    [JsonPropertyName("numBossPacks")]
    public List<int> NumBossPacks { get; init; } = new();

    [JsonPropertyName("superuniques")]
    public List<string> SuperUniques { get; init; } = new();

    /// <summary>The English zone name, or empty string if the feed lacks an enUS key.</summary>
    public string EnglishName => Zone.TryGetValue("enUS", out var n) ? n : string.Empty;
}
