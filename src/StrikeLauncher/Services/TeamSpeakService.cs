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

    private static string SoundPacksDirectory => Path.Combine(Ts3DataDir, "SoundPacks");

    private const string SilentSoundPackName = "StrikeLauncher-Silent";

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
    /// Hands the .ts3plugin file to the TeamSpeak client, which shows its own native
    /// "install this plugin?" confirmation. This is not silent, but it's the reliable
    /// path across client versions - manually parsing/copying the plugin package
    /// (a package.ini-driven zip) risks placing files in the wrong spot or breaking
    /// on a future client update.
    /// </summary>
    public static void InstallPlugin(string ts3ClientExePath, string ts3PluginFilePath)
    {
        Process.Start(new ProcessStartInfo(ts3ClientExePath, $"\"{ts3PluginFilePath}\"")
        {
            UseShellExecute = true
        });
    }

    public static void LaunchAndConnect(string ts3ClientExePath, TeamSpeakServerInfo server, string? nickname)
    {
        var uri = $"ts3server://{server.Host}?port={server.Port}";
        if (!string.IsNullOrWhiteSpace(server.Password)) uri += $"&password={Uri.EscapeDataString(server.Password)}";
        if (!string.IsNullOrWhiteSpace(nickname)) uri += $"&nickname={Uri.EscapeDataString(nickname)}";

        // ts3server:// is a registered URI scheme handled by the TS3 client itself, so
        // this both launches TeamSpeak (if needed) and connects, without needing the exe path.
        _ = ts3ClientExePath;
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }

    /// <summary>
    /// Best-effort: creates a silent sound pack and, if the settings.db schema looks
    /// as expected, switches the client to it. TeamSpeak's settings.db layout isn't
    /// officially documented, so every write is guarded, backed up first, and skipped
    /// entirely if anything looks unfamiliar - worst case the user flips "Sound Pack"
    /// to the created "StrikeLauncher-Silent" entry manually in Options > Notifications.
    /// </summary>
    public static bool TryMuteNotificationSounds()
    {
        try
        {
            EnsureSilentSoundPack();
            return TryPatchSettingsDb();
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureSilentSoundPack()
    {
        var packDir = Path.Combine(SoundPacksDirectory, SilentSoundPackName);
        Directory.CreateDirectory(packDir);

        string[] eventNames =
        {
            "connectionEstablished", "connectionLost", "connectionDisconnected",
            "serverEditedSelf", "serverEditedOther",
            "userEnteredConnect", "userEnteredMoved", "userLeftConnect", "userLeftMoved",
            "userLeftKicked", "userLeftBanned", "userLeftFail",
            "channelKicked", "serverKicked", "serverBanned",
            "textMessageReceived", "channelMessageReceived", "serverMessageReceived",
            "sound_subscription_added", "talkStarted", "talkFinished",
            "microphoneMuted", "microphoneUnmuted", "soundMuted", "soundUnmuted",
            "awayActivated", "awayDeactivated", "recordStarted", "recordStopped",
            "errorSound"
        };

        foreach (var name in eventNames)
        {
            var wavPath = Path.Combine(packDir, name + ".wav");
            if (!File.Exists(wavPath)) WriteSilentWav(wavPath);
        }
    }

    private static void WriteSilentWav(string path)
    {
        const int sampleRate = 44100;
        const short channels = 1;
        const short bitsPerSample = 16;
        const int durationMs = 50;

        var dataLength = sampleRate * channels * (bitsPerSample / 8) * durationMs / 1000;
        var byteRate = sampleRate * channels * (bitsPerSample / 8);
        var blockAlign = (short)(channels * (bitsPerSample / 8));

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
    }

    private static bool TryPatchSettingsDb()
    {
        var dbPath = Path.Combine(Ts3DataDir, "settings.db");
        if (!File.Exists(dbPath)) return false;

        var backupPath = dbPath + ".strikelauncher.bak";
        if (!File.Exists(backupPath)) File.Copy(dbPath, backupPath);

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        if (!TableExists(connection, "preferences")) return false;

        using var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT key FROM preferences WHERE key LIKE '%SoundPack%' OR key LIKE '%PlaySound%'";
        using var reader = selectCmd.ExecuteReader();

        var matchedKeys = new List<string>();
        while (reader.Read()) matchedKeys.Add(reader.GetString(0));
        reader.Close();

        if (matchedKeys.Count == 0) return false;

        foreach (var key in matchedKeys)
        {
            using var updateCmd = connection.CreateCommand();
            var isSoundPackKey = key.Contains("SoundPack", StringComparison.OrdinalIgnoreCase);
            updateCmd.CommandText = "UPDATE preferences SET value = $value WHERE key = $key";
            updateCmd.Parameters.AddWithValue("$value", isSoundPackKey ? SilentSoundPackName : "0");
            updateCmd.Parameters.AddWithValue("$key", key);
            updateCmd.ExecuteNonQuery();
        }

        return true;
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name = $name";
        cmd.Parameters.AddWithValue("$name", tableName);
        return cmd.ExecuteScalar() is not null;
    }
}
