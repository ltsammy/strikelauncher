using System.Net.Http;
using System.Text.Json;
using System.Threading;
using StrikeLauncher.Models;

namespace StrikeLauncher.Services;

/// <summary>
/// Fetches and parses the required-mods feed: a "workshopAddons" JSON array of
/// {id, name} pairs, backed by a real database on the server side (replaced an earlier
/// modlist.html scrape, which broke whenever the exported file got overwritten by a
/// deploy).
/// </summary>
public sealed class ModlistService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public ModlistService(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<ModEntry>> FetchAsync(string url, CancellationToken ct = default)
    {
        var json = await _http.GetStringNoCacheAsync(url, ct);
        return Parse(json);
    }

    public static IReadOnlyList<ModEntry> Parse(string json)
    {
        var response = JsonSerializer.Deserialize<WorkshopAddonsResponse>(json, JsonOptions);
        if (response?.WorkshopAddons is null) return Array.Empty<ModEntry>();

        var mods = new List<ModEntry>();
        var seenIds = new HashSet<ulong>();

        foreach (var addon in response.WorkshopAddons)
        {
            if (!ulong.TryParse(addon.Id, out var workshopId)) continue;
            if (!seenIds.Add(workshopId)) continue;

            var name = string.IsNullOrWhiteSpace(addon.Name) ? workshopId.ToString() : addon.Name.Trim();
            mods.Add(new ModEntry(name, workshopId));
        }

        return mods;
    }
}
