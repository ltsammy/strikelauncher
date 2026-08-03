using System.Net.Http;
using System.Text.Json;
using System.Threading;
using StrikeLauncher.Models;

namespace StrikeLauncher.Services;

public sealed class ServerDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public ServerDataService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ServerData> FetchAsync(string url, CancellationToken ct = default)
    {
        var json = await _http.GetStringAsync(url, ct);
        var data = JsonSerializer.Deserialize<ServerData>(json, JsonOptions) ?? new ServerData();

        // Some feeds bake "host:port" into the host field itself (in addition to a
        // separate Port property) - strip it so URI building doesn't double it up.
        data.TeamSpeak.Host = StripPort(data.TeamSpeak.Host.Trim());
        data.Arma3.Ip = StripPort(data.Arma3.Ip.Trim());

        // A stray trailing space/newline pasted into a CMS text field is an easy mistake
        // to make and invisible in most UIs - trim defensively so it can't silently break
        // the Arma3 -password= / TS3 connect password match.
        data.Arma3.Password = data.Arma3.Password.Trim();
        data.TeamSpeak.Password = data.TeamSpeak.Password.Trim();

        return data;
    }

    private static string StripPort(string hostMaybeWithPort)
    {
        var idx = hostMaybeWithPort.LastIndexOf(':');
        if (idx > 0 && idx < hostMaybeWithPort.Length - 1 && hostMaybeWithPort[(idx + 1)..].All(char.IsDigit))
        {
            return hostMaybeWithPort[..idx];
        }

        return hostMaybeWithPort;
    }
}
