using System.Diagnostics;
using System.IO;
using StrikeLauncher.Models;

namespace StrikeLauncher.Services;

public sealed class GameLauncherService
{
    /// <summary>
    /// Launches Arma 3 directly (bypassing the Steam launch-options roundtrip) with
    /// the flags needed for the fastest possible cold start, the resolved mod set,
    /// and - if server info is supplied - a direct connect.
    /// </summary>
    public static Process? Launch(string arma3ExePath, string modParameter, ArmaServerInfo? server, string? playerNickname)
    {
        var args = new List<string> { "-noSplash", "-skipIntro", "-noPause", "-noLogs", "-hugePages" };

        if (!string.IsNullOrWhiteSpace(modParameter))
        {
            args.Add($"-mod=\"{modParameter}\"");
        }

        if (server is not null && !string.IsNullOrWhiteSpace(server.Ip))
        {
            args.Add($"-connect={server.Ip}");
            args.Add($"-port={server.Port}");
            if (!string.IsNullOrWhiteSpace(server.Password))
            {
                args.Add($"-password={server.Password}");
            }
        }

        if (!string.IsNullOrWhiteSpace(playerNickname))
        {
            args.Add($"-name=\"{playerNickname}\"");
        }

        return Process.Start(new ProcessStartInfo(arma3ExePath, string.Join(' ', args))
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(arma3ExePath) ?? string.Empty
        });
    }
}
