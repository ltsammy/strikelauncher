using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using HtmlAgilityPack;
using StrikeLauncher.Models;

namespace StrikeLauncher.Services;

/// <summary>
/// Fetches and parses an Arma 3 "modlist.html" (the format exported by the official
/// Arma 3 Launcher / ArmA3Sync: a list of &lt;a&gt; tags linking to Steam Workshop
/// "filedetails" pages). Robust to minor structural differences since it just scans
/// every anchor tag for a workshop id instead of relying on a fixed DOM shape.
/// </summary>
public sealed class ModlistService
{
    private static readonly Regex WorkshopIdPattern = new(@"filedetails/\?id=(\d+)", RegexOptions.Compiled);

    private readonly HttpClient _http;

    public ModlistService(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<ModEntry>> FetchAsync(string url, CancellationToken ct = default)
    {
        var html = await _http.GetStringAsync(url, ct);
        return Parse(html);
    }

    public static IReadOnlyList<ModEntry> Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var mods = new List<ModEntry>();
        var seenIds = new HashSet<ulong>();

        var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchors is null) return mods;

        foreach (var anchor in anchors)
        {
            var href = anchor.GetAttributeValue("href", string.Empty);
            var match = WorkshopIdPattern.Match(href);
            if (!match.Success) continue;

            var workshopId = ulong.Parse(match.Groups[1].Value);
            if (!seenIds.Add(workshopId)) continue;

            var name = anchor.GetAttributeValue("data-name", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = HtmlEntity.DeEntitize(anchor.InnerText)?.Trim() ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = workshopId.ToString();
            }

            mods.Add(new ModEntry(name, workshopId));
        }

        return mods;
    }
}
