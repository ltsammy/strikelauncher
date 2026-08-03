using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using HtmlAgilityPack;
using StrikeLauncher.Models;

namespace StrikeLauncher.Services;

/// <summary>
/// Fetches and parses an Arma 3 "modlist.html" as exported by the official Arma 3
/// Launcher: a &lt;tr data-type="ModContainer"&gt; per mod, with the display name in a
/// sibling &lt;td data-type="DisplayName"&gt; and the Workshop link in a separate
/// &lt;a href="...filedetails/?id=..."&gt; whose visible text is just the URL again (no
/// usable name on the anchor itself).
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

        var rows = doc.DocumentNode.SelectNodes("//tr[@data-type='ModContainer']");
        if (rows is not null)
        {
            foreach (var row in rows)
            {
                var link = row.SelectSingleNode(".//a[@href]");
                if (link is null) continue;

                var match = WorkshopIdPattern.Match(link.GetAttributeValue("href", string.Empty));
                if (!match.Success) continue;

                var workshopId = ulong.Parse(match.Groups[1].Value);
                if (!seenIds.Add(workshopId)) continue;

                var nameNode = row.SelectSingleNode(".//td[@data-type='DisplayName']");
                var name = nameNode is not null ? CleanName(nameNode.InnerText) : string.Empty;
                mods.Add(new ModEntry(string.IsNullOrWhiteSpace(name) ? workshopId.ToString() : name, workshopId));
            }
        }

        if (mods.Count > 0) return mods;

        // Fallback for other modlist.html generators (ArmA3Sync, hand-rolled exports, ...)
        // that don't use the official ModContainer/DisplayName row layout.
        var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchors is null) return mods;

        foreach (var anchor in anchors)
        {
            var match = WorkshopIdPattern.Match(anchor.GetAttributeValue("href", string.Empty));
            if (!match.Success) continue;

            var workshopId = ulong.Parse(match.Groups[1].Value);
            if (!seenIds.Add(workshopId)) continue;

            var name = anchor.GetAttributeValue("data-name", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                var innerText = CleanName(anchor.InnerText);
                // the official export repeats the raw URL as the link text - never show that as a "name"
                if (!innerText.StartsWith("http", StringComparison.OrdinalIgnoreCase)) name = innerText;
            }

            mods.Add(new ModEntry(string.IsNullOrWhiteSpace(name) ? workshopId.ToString() : name, workshopId));
        }

        return mods;
    }

    private static string CleanName(string rawInnerText) =>
        HtmlEntity.DeEntitize(rawInnerText)?.Trim() ?? string.Empty;
}
