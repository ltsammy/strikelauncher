using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;
using StrikeLauncher.Models;

namespace StrikeLauncher.Services;

public sealed class TeamSpeakService
{
    private static string Ts3DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TS3Client");

    private static string PluginsDirectory => Path.Combine(Ts3DataDir, "plugins");

    // TeamSpeak's built-in "Sounds deactivated" option in Options > Notifications - not a
    // custom soundpack, just this literal value under the key "SoundPack". Confirmed against
    // a real settings.db: TS3 duplicates this same key/value across three section tables.
    private static readonly string[] SoundPackTables = { "Notifications", "Application", "General" };
    private const string MutedSoundPackValue = "nosounds";

    public static string GetClientExePath(string installPath) => Path.Combine(installPath, "ts3client_win64.exe");

    public static bool IsInstalled(string? installPath) =>
        !string.IsNullOrWhiteSpace(installPath) && File.Exists(GetClientExePath(installPath));

    public static bool IsPluginInstalled(string dllNameHint)
    {
        if (!Directory.Exists(PluginsDirectory)) return false;

        return Directory.EnumerateFiles(PluginsDirectory, "*.dll")
            .Any(f => Path.GetFileNameWithoutExtension(f).Contains(dllNameHint, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Opens the .ts3_plugin file the same way double-clicking it in Explorer would -
    /// via its registered file association - which shows TeamSpeak's native "install
    /// this plugin?" confirmation. Deliberately not invoking ts3client_win64.exe
    /// ourselves with the file as an argument: that assumes an internal command-line
    /// shape we don't actually know, whereas the file association is the documented,
    /// user-facing way to install a .ts3_plugin and always works.
    /// </summary>
    public static void InstallPlugin(string ts3PluginFilePath)
    {
        Process.Start(new ProcessStartInfo(ts3PluginFilePath) { UseShellExecute = true });
    }

    /// <summary>
    /// Closes any running TeamSpeak client instances: asks nicely first (CloseMainWindow,
    /// like clicking the window's X), then force-kills anything still around after a short
    /// grace period. Used to shut TeamSpeak down together with Arma 3. Returns false if
    /// TeamSpeak wasn't even running, so the caller can log something more useful than
    /// silence.
    /// </summary>
    public static async Task<bool> CloseAllInstancesAsync()
    {
        if (Process.GetProcessesByName("ts3client_win64").Length == 0) return false;

        foreach (var process in Process.GetProcessesByName("ts3client_win64"))
        {
            using (process)
            {
                try
                {
                    process.CloseMainWindow();
                }
                catch
                {
                    // process may have already exited between the enumeration and this call
                }
            }
        }

        await Task.Delay(3000);

        foreach (var process in Process.GetProcessesByName("ts3client_win64"))
        {
            using (process)
            {
                try
                {
                    if (!process.HasExited) process.Kill();
                }
                catch
                {
                    // already exited, or we don't have permission to kill it - nothing more we can do
                }
            }
        }

        return true;
    }

    public static void LaunchAndConnect(string ts3ClientExePath, TeamSpeakServerInfo server)
    {
        var uri = $"ts3server://{server.Host}?port={server.Port}";
        if (!string.IsNullOrWhiteSpace(server.Password)) uri += $"&password={Uri.EscapeDataString(server.Password)}";

        // Pass the ts3server:// string directly as an argument to the client exe instead
        // of relying on Windows having that URI scheme registered (UseShellExecute=true
        // throws "Anwendung nicht gefunden" / Win32Exception on installs where it isn't -
        // confirmed happening on a real player's machine even with TS3 properly installed).
        // TS3 parses this exact string from its own argv regardless of protocol registration.
        Process.Start(new ProcessStartInfo(ts3ClientExePath, $"\"{uri}\"") { UseShellExecute = false });
    }

    /// <summary>
    /// Switches TeamSpeak to its own built-in "Sounds deactivated" option (Options >
    /// Notifications > Sound Pack). Confirmed against a real settings.db: stored as
    /// key "SoundPack" = "nosounds", duplicated across the Notifications/Application/
    /// General section tables (all three get updated to match what TS3 itself does when
    /// you pick that option from the UI). settings.db is backed up first regardless.
    /// </summary>
    public static bool TryMuteNotificationSounds()
    {
        try
        {
            return TryPatchSettingsDb();
        }
        catch
        {
            return false;
        }
    }

    private static bool TryPatchSettingsDb()
    {
        var dbPath = Path.Combine(Ts3DataDir, "settings.db");
        if (!File.Exists(dbPath)) return false;

        var backupPath = dbPath + ".strikelauncher.bak";
        if (!File.Exists(backupPath)) File.Copy(dbPath, backupPath);

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var anyChanged = false;

        foreach (var table in SoundPackTables)
        {
            if (!TableExists(connection, table)) continue;

            using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.CommandText = $"UPDATE '{table}' SET value = $value, timestamp = $ts WHERE key = 'SoundPack'";
                updateCmd.Parameters.AddWithValue("$value", MutedSoundPackValue);
                updateCmd.Parameters.AddWithValue("$ts", timestamp);

                if (updateCmd.ExecuteNonQuery() > 0)
                {
                    anyChanged = true;
                    continue;
                }
            }

            using (var insertCmd = connection.CreateCommand())
            {
                insertCmd.CommandText = $"INSERT INTO '{table}' (timestamp, key, value) VALUES ($ts, 'SoundPack', $value)";
                insertCmd.Parameters.AddWithValue("$ts", timestamp);
                insertCmd.Parameters.AddWithValue("$value", MutedSoundPackValue);
                insertCmd.ExecuteNonQuery();
                anyChanged = true;
            }
        }

        return anyChanged;
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name = $name";
        cmd.Parameters.AddWithValue("$name", tableName);
        return cmd.ExecuteScalar() is not null;
    }
}
