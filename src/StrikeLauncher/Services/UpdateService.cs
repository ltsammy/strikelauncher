using Velopack;
using Velopack.Sources;

namespace StrikeLauncher.Services;

public sealed class UpdateService
{
    private readonly string _githubRepoUrl;

    public UpdateService(string githubRepoUrl)
    {
        _githubRepoUrl = githubRepoUrl;
    }

    public async Task<bool> CheckAndApplyAsync(Action<string>? onStatus = null)
    {
        if (string.IsNullOrWhiteSpace(_githubRepoUrl)) return false;

        try
        {
            var manager = new UpdateManager(new GithubSource(_githubRepoUrl, null, false));
            if (!manager.IsInstalled)
            {
                onStatus?.Invoke("Kein installiertes Paket erkannt (Entwicklungsmodus) - Update-Check übersprungen.");
                return false;
            }

            var update = await manager.CheckForUpdatesAsync();
            if (update is null)
            {
                onStatus?.Invoke("Launcher ist aktuell.");
                return false;
            }

            onStatus?.Invoke($"Update {update.TargetFullRelease.Version} wird geladen...");
            await manager.DownloadUpdatesAsync(update);

            onStatus?.Invoke("Update wird installiert, Launcher startet neu...");
            manager.ApplyUpdatesAndRestart(update);
            return true;
        }
        catch (Exception ex)
        {
            onStatus?.Invoke($"Update-Prüfung fehlgeschlagen: {ex.Message}");
            return false;
        }
    }
}
