using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows.Media.Imaging;

namespace StrikeLauncher.Services;

/// <summary>
/// Downloads the community background artwork referenced by launcher.json and caches
/// it on disk, so the launcher can show it instantly on the next start (before the
/// network round-trip completes) instead of a blank window.
/// </summary>
public sealed class BackgroundImageService
{
    private static string CacheDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StrikeLauncher", "cache");

    private static string CachePath => Path.Combine(CacheDir, "background.img");

    private readonly HttpClient _http;

    public BackgroundImageService(HttpClient http)
    {
        _http = http;
    }

    public BitmapImage? LoadCached()
    {
        if (!File.Exists(CachePath)) return null;

        try
        {
            return CreateFrozenBitmap(File.ReadAllBytes(CachePath));
        }
        catch
        {
            return null;
        }
    }

    public async Task<BitmapImage?> FetchAndCacheAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var bytes = await _http.GetByteArrayAsync(url, ct);
        var bitmap = CreateFrozenBitmap(bytes);

        Directory.CreateDirectory(CacheDir);
        await File.WriteAllBytesAsync(CachePath, bytes, ct);

        return bitmap;
    }

    private static BitmapImage CreateFrozenBitmap(byte[] bytes)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = new MemoryStream(bytes);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
