using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace StrikeLauncher.Services;

/// <summary>
/// Auto-detects Steam, Arma 3 and TeamSpeak 3 install locations via the registry
/// and well-known default paths, without requiring the user to type anything in
/// - unless auto-detection fails, in which case the caller falls back to a manual
/// path picked in Settings.
/// </summary>
public sealed class PathDetectionService
{
    public const uint Arma3AppId = 107410;

    public string? FindSteamPath()
    {
        var fromUser = ReadRegistryString(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        if (IsValidDirectory(fromUser)) return NormalizePath(fromUser!);

        var fromMachine64 = ReadRegistryString(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        if (IsValidDirectory(fromMachine64)) return NormalizePath(fromMachine64!);

        var fromMachine32 = ReadRegistryString(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
        if (IsValidDirectory(fromMachine32)) return NormalizePath(fromMachine32!);

        string[] fallbacks =
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam"
        };
        return fallbacks.FirstOrDefault(Directory.Exists);
    }

    public IReadOnlyList<string> FindSteamLibraryRoots(string steamPath)
    {
        var roots = new List<string> { steamPath };

        var vdfCandidates = new[]
        {
            Path.Combine(steamPath, "steamapps", "libraryfolders.vdf"),
            Path.Combine(steamPath, "config", "libraryfolders.vdf")
        };

        foreach (var vdfPath in vdfCandidates)
        {
            if (!File.Exists(vdfPath)) continue;

            var text = File.ReadAllText(vdfPath);
            foreach (Match match in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\""))
            {
                var path = match.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(path) && !roots.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    roots.Add(path);
                }
            }

            break;
        }

        return roots;
    }

    public string? FindArma3Path(string steamPath)
    {
        foreach (var root in FindSteamLibraryRoots(steamPath))
        {
            var candidate = Path.Combine(root, "steamapps", "common", "Arma 3");
            if (File.Exists(Path.Combine(candidate, "arma3_x64.exe")) ||
                File.Exists(Path.Combine(candidate, "arma3.exe")))
            {
                return candidate;
            }
        }

        return null;
    }

    public string? FindWorkshopContentPath(string steamPath, string arma3Path)
    {
        foreach (var root in FindSteamLibraryRoots(steamPath))
        {
            var candidate = Path.Combine(root, "steamapps", "workshop", "content", Arma3AppId.ToString());
            if (Directory.Exists(candidate)) return candidate;

            // arma3Path lives under the same library root - prefer the matching one even if
            // the workshop/content folder doesn't exist yet (it is created on first subscribe).
            if (arma3Path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return candidate;
        }

        return null;
    }

    public string? FindTeamSpeakPath()
    {
        var candidates = new List<string?>
        {
            ReadRegistryString(Registry.CurrentUser, @"Software\TeamSpeak3 Client", "InstallPath"),
            FindFromUninstallKey(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            FindFromUninstallKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            @"C:\Program Files\TeamSpeak 3 Client",
            @"C:\Program Files (x86)\TeamSpeak 3 Client"
        };

        return candidates.FirstOrDefault(p => IsValidDirectory(p) && File.Exists(Path.Combine(p!, "ts3client_win64.exe")));
    }

    private static string? FindFromUninstallKey(RegistryKey hive, string uninstallRoot)
    {
        using var root = hive.OpenSubKey(uninstallRoot);
        if (root is null) return null;

        foreach (var subKeyName in root.GetSubKeyNames())
        {
            using var subKey = root.OpenSubKey(subKeyName);
            var displayName = subKey?.GetValue("DisplayName") as string;
            if (displayName is null || !displayName.Contains("TeamSpeak 3 Client", StringComparison.OrdinalIgnoreCase))
                continue;

            var installLocation = subKey?.GetValue("InstallLocation") as string;
            if (IsValidDirectory(installLocation)) return installLocation;
        }

        return null;
    }

    private static string? ReadRegistryString(RegistryKey hive, string subKeyPath, string valueName)
    {
        using var key = hive.OpenSubKey(subKeyPath);
        return key?.GetValue(valueName) as string;
    }

    private static bool IsValidDirectory(string? path) => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    private static string NormalizePath(string path) => path.Replace('/', '\\');
}
