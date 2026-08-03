using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StrikeLauncher.Services;

public enum SubscribeOutcome
{
    Success,
    TimedOut,
    SteamNotRunning
}

[Flags]
internal enum EItemState : uint
{
    None = 0,
    Subscribed = 1,
    LegacyItem = 2,
    Installed = 4,
    NeedsUpdate = 8,
    Downloading = 16,
    DownloadPending = 32,
    DisabledLocally = 64
}

internal enum ESteamAPIInitResult
{
    Ok = 0,
    FailedGeneric = 1,
    NoSteamClient = 2,
    VersionMismatch = 3
}

/// <summary>
/// Talks to steam_api64.dll directly through its "flat" C exports instead of going through
/// the Steamworks.NET wrapper. Steamworks SDKs from roughly mid-2024 onward (this one is
/// v1.65) no longer export the classic SteamAPI_Init()/SteamUGC() helpers Steamworks.NET
/// P/Invokes - only SteamAPI_InitFlat and the SteamAPI_ISteamUGC_* flat entry points remain
/// (verified against this exact DLL's export table; SteamAPI_Init is a C++-header-only
/// inline in newer SDKs, not something you can P/Invoke by name anymore).
///
/// This subscribes to (and force-downloads) missing Workshop items directly, without
/// sending the user to a browser/Steam overlay. Requires Steam to be running and the
/// account to own Arma 3 (appid 107410, see steam_appid.txt next to the executable).
/// </summary>
public sealed class SteamWorkshopService : IDisposable
{
    private const string SteamApiDll = "steam_api64";

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int SteamAPI_InitFlat(StringBuilder? errMsg);

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SteamAPI_RunCallbacks();

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SteamAPI_Shutdown();

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SteamAPI_SteamUGC_v021();

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SteamAPI_SteamFriends_v018();

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SteamAPI_ISteamFriends_GetPersonaName(IntPtr self);

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int SteamAPI_ISteamFriends_GetMediumFriendAvatar(IntPtr self, ulong steamIdFriend);

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SteamAPI_SteamUser_v023();

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong SteamAPI_ISteamUser_GetSteamID(IntPtr self);

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SteamAPI_SteamUtils_v011();

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool SteamAPI_ISteamUtils_GetImageSize(IntPtr self, int image, out uint width, out uint height);

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool SteamAPI_ISteamUtils_GetImageRGBA(IntPtr self, int image, byte[] destBuffer, int destBufferSize);

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong SteamAPI_ISteamUGC_SubscribeItem(IntPtr self, ulong publishedFileId);

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint SteamAPI_ISteamUGC_GetItemState(IntPtr self, ulong publishedFileId);

    [DllImport(SteamApiDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool SteamAPI_ISteamUGC_DownloadItem(IntPtr self, ulong publishedFileId, [MarshalAs(UnmanagedType.U1)] bool highPriority);

    private Timer? _callbackPump;
    private IntPtr _ugc;
    private IntPtr _friends;

    public bool IsInitialized { get; private set; }

    public string? LastError { get; private set; }

    public bool Initialize()
    {
        try
        {
            var errMsg = new StringBuilder(1024);
            var result = (ESteamAPIInitResult)SteamAPI_InitFlat(errMsg);
            IsInitialized = result == ESteamAPIInitResult.Ok;
            LastError = IsInitialized ? null : (errMsg.Length > 0 ? errMsg.ToString() : result.ToString());
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            IsInitialized = false;
            LastError = ex.Message;
        }

        if (IsInitialized)
        {
            _ugc = SteamAPI_SteamUGC_v021();
            if (_ugc == IntPtr.Zero)
            {
                IsInitialized = false;
                LastError = "SteamUGC-Interface nicht verfügbar (Steam-Client zu alt?).";
                return false;
            }

            _friends = SteamAPI_SteamFriends_v018();
            _callbackPump = new Timer(_ => SteamAPI_RunCallbacks(), null, 0, 100);
        }

        return IsInitialized;
    }

    public string? GetPersonaName()
    {
        if (!IsInitialized || _friends == IntPtr.Zero) return null;

        var ptr = SteamAPI_ISteamFriends_GetPersonaName(_friends);
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }

    /// <summary>
    /// The local user's own avatar is preloaded by the Steam client, so - unlike friends'
    /// avatars - it's available synchronously right after init, no AvatarImageLoaded_t
    /// callback wait needed.
    /// </summary>
    public BitmapSource? GetAvatarImage()
    {
        if (!IsInitialized || _friends == IntPtr.Zero) return null;

        try
        {
            var user = SteamAPI_SteamUser_v023();
            var utils = SteamAPI_SteamUtils_v011();
            if (user == IntPtr.Zero || utils == IntPtr.Zero) return null;

            var steamId = SteamAPI_ISteamUser_GetSteamID(user);
            var imageHandle = SteamAPI_ISteamFriends_GetMediumFriendAvatar(_friends, steamId);
            if (imageHandle <= 0) return null;

            if (!SteamAPI_ISteamUtils_GetImageSize(utils, imageHandle, out var width, out var height) || width == 0 || height == 0)
                return null;

            var buffer = new byte[width * height * 4];
            if (!SteamAPI_ISteamUtils_GetImageRGBA(utils, imageHandle, buffer, buffer.Length))
                return null;

            for (var i = 0; i < buffer.Length; i += 4)
            {
                (buffer[i], buffer[i + 2]) = (buffer[i + 2], buffer[i]); // RGBA -> BGRA
            }

            var bitmap = BitmapSource.Create((int)width, (int)height, 96, 96, PixelFormats.Bgra32, null, buffer, (int)width * 4);
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public async Task<SubscribeOutcome> SubscribeAndInstallAsync(
        ulong workshopId,
        TimeSpan timeout,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsInitialized) return SubscribeOutcome.SteamNotRunning;

        SteamAPI_ISteamUGC_SubscribeItem(_ugc, workshopId);
        progress?.Report($"Abonniere {workshopId}...");

        var deadline = DateTime.UtcNow + timeout;
        var downloadTriggered = false;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var state = (EItemState)SteamAPI_ISteamUGC_GetItemState(_ugc, workshopId);

            if (state.HasFlag(EItemState.Installed) && !state.HasFlag(EItemState.NeedsUpdate))
            {
                return SubscribeOutcome.Success;
            }

            if (state.HasFlag(EItemState.Subscribed) && !downloadTriggered)
            {
                SteamAPI_ISteamUGC_DownloadItem(_ugc, workshopId, true);
                downloadTriggered = true;
                progress?.Report($"Lade {workshopId} herunter...");
            }

            await Task.Delay(500, ct);
        }

        return SubscribeOutcome.TimedOut;
    }

    /// <summary>
    /// Tears the Steam session back down. Steam's overlay attaches to any process that
    /// successfully calls SteamAPI_Init while the client is running (there's no public
    /// API to opt a process out of it) - the overlay hook has been observed rendering
    /// glitchy/broken in this WPF window, so the launcher only keeps a session open for
    /// as long as it actually needs one (a quick profile fetch, or an active subscribe)
    /// instead of for its entire lifetime, to minimize how long that hook has to attach.
    /// </summary>
    public void Shutdown()
    {
        _callbackPump?.Dispose();
        _callbackPump = null;

        if (IsInitialized)
        {
            SteamAPI_Shutdown();
        }

        IsInitialized = false;
        _ugc = IntPtr.Zero;
        _friends = IntPtr.Zero;
    }

    public void Dispose() => Shutdown();
}
