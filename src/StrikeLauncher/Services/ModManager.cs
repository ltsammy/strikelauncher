using System.IO;
using StrikeLauncher.Models;

namespace StrikeLauncher.Services;

/// <summary>
/// Compares required mods against what's already present in the Steam Workshop
/// content folder for Arma 3 (steamapps/workshop/content/107410/&lt;id&gt;), which is
/// where Steam places subscribed items regardless of whether Steam is currently running.
/// </summary>
public sealed class ModManager
{
    public static bool IsInstalled(ModEntry mod, string workshopContentPath) =>
        Directory.Exists(Path.Combine(workshopContentPath, mod.WorkshopId.ToString()));

    public static IReadOnlyList<ModEntry> GetMissing(IEnumerable<ModEntry> required, string workshopContentPath) =>
        required.Where(mod => !IsInstalled(mod, workshopContentPath)).ToList();

    public static string BuildModParameter(IEnumerable<ModEntry> required, string workshopContentPath)
    {
        var installedPaths = required
            .Select(mod => Path.Combine(workshopContentPath, mod.WorkshopId.ToString()))
            .Where(Directory.Exists);

        return string.Join(';', installedPaths);
    }
}
