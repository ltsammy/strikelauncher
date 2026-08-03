using System.Net.Sockets;
using System.Threading;

namespace StrikeLauncher.Services;

/// <summary>
/// Best-effort "is the server up" checks - not full monitoring, just a quick signal for the UI.
/// </summary>
public sealed class ServerStatusService
{
    /// <summary>
    /// Arma 3 (and other Source-engine-query-compatible servers) answer a UDP A2S_INFO
    /// request on gamePort + 1 by default. A valid reply is enough to call it "online" -
    /// we don't need to parse map/player count for a simple status indicator.
    /// </summary>
    public static async Task<bool> CheckArma3Async(string ip, int gamePort, TimeSpan timeout, CancellationToken ct = default)
    {
        try
        {
            using var udp = new UdpClient();
            var request = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }.Concat("TSource Engine Query\0"u8.ToArray()).ToArray();
            await udp.SendAsync(request, request.Length, ip, gamePort + 1);

            var receiveTask = udp.ReceiveAsync();
            var completed = await Task.WhenAny(receiveTask, Task.Delay(timeout, ct));
            if (completed != receiveTask || !receiveTask.IsCompletedSuccessfully) return false;

            var buffer = receiveTask.Result.Buffer;
            return buffer.Length > 4 && buffer[0] == 0xFF && buffer[1] == 0xFF && buffer[2] == 0xFF && buffer[3] == 0xFF;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Heuristic only: tries a plain TCP connect to the default ServerQuery port (10011).
    /// Many self-hosted TS3 servers leave this reachable, but some hosts firewall it off
    /// entirely - a "false" here doesn't reliably mean the voice server itself is down.
    /// </summary>
    public static async Task<bool> CheckTeamSpeakAsync(string host, TimeSpan timeout, CancellationToken ct = default)
    {
        const int queryPort = 10011;

        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(host, queryPort, ct).AsTask();
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeout, ct));
            return completed == connectTask && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }
}
