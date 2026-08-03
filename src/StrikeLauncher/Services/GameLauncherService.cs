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
    public static Process? Launch(string arma3ExePath, IEnumerable<string> modPaths, ArmaServerInfo? server)
    {
        var args = new List<string> { "-noSplash", "-skipIntro", "-noPause", "-noLogs", "-hugePages" };

        // One -mod= flag per path instead of a single "-mod=\"a;b;c\"" - avoids any
        // ambiguity around quoting a semicolon-separated list, and each path still gets
        // its own quotes in case a Steam library lives under a directory with a space
        // in it (e.g. "Program Files (x86)").
        foreach (var modPath in modPaths)
        {
            args.Add($"-mod=\"{modPath}\"");
        }

        if (server is not null && !string.IsNullOrWhiteSpace(server.Ip))
        {
            args.Add($"-connect={server.Ip}");
            args.Add($"-port={server.Port}");
            if (!string.IsNullOrWhiteSpace(server.Password))
            {
                args.Add($"-password=\"{server.Password}\"");
            }
        }

        return Process.Start(new ProcessStartInfo(arma3ExePath, string.Join(' ', args))
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(arma3ExePath) ?? string.Empty
        });
    }
}
