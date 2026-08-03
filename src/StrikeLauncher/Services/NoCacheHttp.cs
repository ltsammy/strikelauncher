using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace StrikeLauncher.Services;

/// <summary>
/// Plain HttpClient never caches responses client-side, but that doesn't cover
/// intermediaries we don't control (a CDN in front of the site, a corporate proxy,
/// ...) - explicit no-cache headers make sure every fetch is genuinely live instead
/// of relying on nobody in the chain deciding to cache it.
/// </summary>
internal static class NoCacheHttp
{
    public static async Task<string> GetStringNoCacheAsync(this HttpClient http, string url, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
