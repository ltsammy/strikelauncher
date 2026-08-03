using System.IO;
using System.Text.Json;
using StrikeLauncher.Models;

namespace StrikeLauncher.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static string SettingsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StrikeLauncher");

    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    private static string BundledDefaultConfigPath => Path.Combine(AppContext.BaseDirectory, "config", "default-config.json");

    public AppSettings Load()
    {
        if (File.Exists(SettingsPath))
        {
            try
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is not null) return settings;
            }
            catch (JsonException)
            {
                // Fall through to defaults below; a corrupt settings.json shouldn't crash the launcher.
            }
        }

        return LoadBundledDefaults();
    }

    private static AppSettings LoadBundledDefaults()
    {
        if (File.Exists(BundledDefaultConfigPath))
        {
            try
            {
                var json = File.ReadAllText(BundledDefaultConfigPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is not null) return settings;
            }
            catch (JsonException)
            {
                // Ignore and fall back to a bare default below.
            }
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
