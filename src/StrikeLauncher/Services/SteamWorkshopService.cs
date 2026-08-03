using System.Threading;
using Steamworks;

namespace StrikeLauncher.Services;

public enum SubscribeOutcome
{
    Success,
    TimedOut,
    SteamNotRunning
}

/// <summary>
/// Wraps the Steamworks client API to subscribe to (and force-download) missing
/// Workshop items directly, without sending the user to a browser/Steam overlay.
/// Requires Steam to be running and the account to own Arma 3 (appid 107410, see
/// steam_appid.txt next to the executable). This is the same client-side trick used
/// by most third-party workshop mod managers - there is no public Web API to trigger
/// a subscribe server-side.
/// </summary>
public sealed class SteamWorkshopService : IDisposable
{
    private Timer? _callbackPump;

    public bool IsInitialized { get; private set; }

    public bool Initialize()
    {
        try
        {
            IsInitialized = SteamAPI.Init();
        }
        catch (DllNotFoundException)
        {
            IsInitialized = false;
        }

        if (IsInitialized)
        {
            _callbackPump = new Timer(_ => SteamAPI.RunCallbacks(), null, 0, 100);
        }

        return IsInitialized;
    }

    public async Task<SubscribeOutcome> SubscribeAndInstallAsync(
        ulong workshopId,
        TimeSpan timeout,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsInitialized) return SubscribeOutcome.SteamNotRunning;

        var id = new PublishedFileId_t(workshopId);
        SteamUGC.SubscribeItem(id);
        progress?.Report($"Abonniere {workshopId}...");

        var deadline = DateTime.UtcNow + timeout;
        var downloadTriggered = false;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var state = (EItemState)SteamUGC.GetItemState(id);

            if (state.HasFlag(EItemState.k_EItemStateInstalled) &&
                !state.HasFlag(EItemState.k_EItemStateNeedsUpdate))
            {
                return SubscribeOutcome.Success;
            }

            if (state.HasFlag(EItemState.k_EItemStateSubscribed) && !downloadTriggered)
            {
                SteamUGC.DownloadItem(id, true);
                downloadTriggered = true;
                progress?.Report($"Lade {workshopId} herunter...");
            }

            await Task.Delay(500, ct);
        }

        return SubscribeOutcome.TimedOut;
    }

    public void Dispose()
    {
        _callbackPump?.Dispose();
        if (IsInitialized)
        {
            SteamAPI.Shutdown();
        }
    }
}
