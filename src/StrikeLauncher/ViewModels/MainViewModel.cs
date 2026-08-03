using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StrikeLauncher.Models;
using StrikeLauncher.Services;

namespace StrikeLauncher.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly HttpClient _http = new();
    private readonly SettingsService _settingsService = new();
    private readonly PathDetectionService _pathDetection = new();
    private readonly ModlistService _modlistService;
    private readonly ServerDataService _serverDataService;
    private readonly SteamWorkshopService _steamWorkshop = new();
    private readonly BackgroundImageService _backgroundImageService;

    private readonly CancellationTokenSource _serverStatusCts = new();

    private AppSettings _settings;
    private string? _workshopContentPath;
    private ServerData? _serverData;

    public MainViewModel()
    {
        _modlistService = new ModlistService(_http);
        _serverDataService = new ServerDataService(_http);
        _backgroundImageService = new BackgroundImageService(_http);
        _settings = _settingsService.Load();

        DetectPaths();
        _ = CheckForUpdatesAsync();
        _ = LoadServerDataAndBackgroundAsync();
        _ = InitializeSteamAsync();
        _ = ServerStatusLoopAsync(_serverStatusCts.Token);
    }

    public ObservableCollection<ModStatusItem> Mods { get; } = new();

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Bereit.";

    [ObservableProperty]
    private bool _isBusy;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanLaunch));

    [ObservableProperty]
    private bool _isArma3Running;

    partial void OnIsArma3RunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanLaunch));
        OnPropertyChanged(nameof(StartButtonLabel));
    }

    public bool CanLaunch => !IsBusy && !IsArma3Running;

    public string StartButtonLabel => IsArma3Running ? "LÄUFT" : "START";

    [ObservableProperty]
    private string? _arma3Path;

    [ObservableProperty]
    private string? _teamSpeakPath;

    [ObservableProperty]
    private bool _ts3PluginInstalled;

    [ObservableProperty]
    private ImageSource? _backgroundImageSource;

    [ObservableProperty]
    private string? _launcherDownloadUrl;

    partial void OnLauncherDownloadUrlChanged(string? value) => OnPropertyChanged(nameof(HasDownloadUrl));

    public bool HasDownloadUrl => !string.IsNullOrWhiteSpace(LauncherDownloadUrl);

    [ObservableProperty]
    private string? _steamUserName;

    [ObservableProperty]
    private ImageSource? _steamAvatar;

    [ObservableProperty]
    private bool? _arma3ServerOnline;

    [ObservableProperty]
    private bool? _teamSpeakServerOnline;

    public AppSettings Settings => _settings;

    [RelayCommand]
    private void DetectPaths()
    {
        _settings.SteamPath ??= _pathDetection.FindSteamPath();

        if (_settings.SteamPath is not null)
        {
            _settings.Arma3Path ??= _pathDetection.FindArma3Path(_settings.SteamPath);
            if (_settings.Arma3Path is not null)
            {
                _workshopContentPath = _pathDetection.FindWorkshopContentPath(_settings.SteamPath, _settings.Arma3Path);
            }
        }

        _settings.TeamSpeakPath ??= _pathDetection.FindTeamSpeakPath();

        Arma3Path = _settings.Arma3Path;
        TeamSpeakPath = _settings.TeamSpeakPath;
        Ts3PluginInstalled = TeamSpeakService.IsPluginInstalled(_settings.Ts3PluginDllHint);

        Log(Arma3Path is null ? "Arma 3 wurde nicht automatisch gefunden - bitte in den Einstellungen setzen." : $"Arma 3 gefunden: {Arma3Path}");
        Log(TeamSpeakPath is null ? "TeamSpeak 3 wurde nicht automatisch gefunden - bitte in den Einstellungen setzen." : $"TeamSpeak 3 gefunden: {TeamSpeakPath}");

        _settingsService.Save(_settings);
    }

    [RelayCommand]
    private async Task CheckModsAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.ModlistUrl))
        {
            Log("Keine Modlist-URL konfiguriert.");
            return;
        }

        IsBusy = true;
        StatusText = "Prüfe Modliste...";
        try
        {
            var required = await _modlistService.FetchAsync(_settings.ModlistUrl);
            Mods.Clear();

            foreach (var mod in required)
            {
                var installed = _workshopContentPath is not null && ModManager.IsInstalled(mod, _workshopContentPath);
                Mods.Add(new ModStatusItem(mod) { Status = installed ? ModStatus.Installed : ModStatus.Missing });
            }

            var missingCount = Mods.Count(m => m.Status == ModStatus.Missing);
            StatusText = missingCount == 0
                ? $"Alle {Mods.Count} Mods sind installiert."
                : $"{missingCount} von {Mods.Count} Mods fehlen.";
            Log(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = "Modliste konnte nicht geladen werden.";
            Log($"Fehler beim Laden der Modliste: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckAndSubscribeModsAsync()
    {
        await CheckModsAsync();
        await SubscribeMissingAsync();
    }

    [RelayCommand]
    private async Task SubscribeMissingAsync()
    {
        if (_workshopContentPath is null)
        {
            Log("Arma 3 Pfad unbekannt - kann Workshop-Ordner nicht bestimmen.");
            return;
        }

        var missing = Mods.Where(m => m.Status == ModStatus.Missing).ToList();
        if (missing.Count == 0) return;

        IsBusy = true;
        try
        {
            if (!_steamWorkshop.IsInitialized && !_steamWorkshop.Initialize())
            {
                Log($"Steam-Init fehlgeschlagen ({_steamWorkshop.LastError ?? "unbekannt"}) - automatisches Abonnieren nicht möglich. Bitte Mods manuell im Steam Workshop abonnieren.");
                return;
            }

            foreach (var item in missing)
            {
                item.Status = ModStatus.Subscribing;
                StatusText = $"Abonniere {item.Name}...";

                var progress = new Progress<string>(Log);
                var outcome = await _steamWorkshop.SubscribeAndInstallAsync(item.WorkshopId, TimeSpan.FromMinutes(10), progress);

                item.Status = outcome == SubscribeOutcome.Success ? ModStatus.Installed : ModStatus.Failed;
                Log($"{item.Name}: {outcome}");
            }

            StatusText = "Fertig mit Abonnieren.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadServerDataAndBackgroundAsync()
    {
        BackgroundImageSource = _backgroundImageService.LoadCached();

        if (string.IsNullOrWhiteSpace(_settings.ServerDataUrl)) return;

        try
        {
            _serverData = await _serverDataService.FetchAsync(_settings.ServerDataUrl);
            LauncherDownloadUrl = _serverData.LauncherDownloadUrl;

            if (!string.IsNullOrWhiteSpace(_serverData.LauncherBackgroundUrl))
            {
                var fresh = await _backgroundImageService.FetchAndCacheAsync(_serverData.LauncherBackgroundUrl);
                if (fresh is not null) BackgroundImageSource = fresh;
            }
        }
        catch (Exception ex)
        {
            Log($"Serverdaten konnten nicht geladen werden: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenDownloadPage()
    {
        if (string.IsNullOrWhiteSpace(LauncherDownloadUrl)) return;
        Process.Start(new ProcessStartInfo(LauncherDownloadUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task PrepareAndLaunchAsync()
    {
        IsBusy = true;
        try
        {
            if (_serverData is null)
            {
                StatusText = "Lade Serverdaten...";
                await LoadServerDataAndBackgroundAsync();
            }

            await CheckModsAsync();
            await SubscribeMissingAsync();

            // TeamSpeak setup failing entirely (network hiccup downloading the plugin,
            // an unexpected exception, ...) must never prevent Arma 3 from launching.
            try
            {
                await PrepareTeamSpeakAsync();
            }
            catch (Exception ex)
            {
                Log($"TeamSpeak-Vorbereitung fehlgeschlagen: {ex.Message}");
            }

            LaunchArma3();
        }
        catch (Exception ex)
        {
            Log($"Fehler beim Start: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PrepareTeamSpeakAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.TeamSpeakPath) || !TeamSpeakService.IsInstalled(_settings.TeamSpeakPath))
        {
            var result = MessageBox.Show(
                "TeamSpeak 3 wurde nicht gefunden. Download-Seite jetzt öffnen?",
                "TeamSpeak 3 fehlt",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo("https://www.teamspeak.com/en/downloads/") { UseShellExecute = true });
            }

            Log("TeamSpeak 3 nicht installiert - Vorbereitung übersprungen.");
            return;
        }

        var ts3Exe = TeamSpeakService.GetClientExePath(_settings.TeamSpeakPath);

        if (!TeamSpeakService.IsPluginInstalled(_settings.Ts3PluginDllHint) && !string.IsNullOrWhiteSpace(_settings.Ts3PluginUrl))
        {
            Log("Lade Task Force Radio Plugin...");
            var tempPluginPath = Path.Combine(Path.GetTempPath(), "task_force_radio.ts3_plugin");
            var bytes = await _http.GetByteArrayAsync(_settings.Ts3PluginUrl);
            await File.WriteAllBytesAsync(tempPluginPath, bytes);

            TeamSpeakService.InstallPlugin(tempPluginPath);
            Log("TS3-Installationsdialog geöffnet.");

            // The plugin dialog needs a human click - block here so Arma 3 never starts
            // before TeamSpeak (and the plugin) are actually ready, matching the intended
            // "TS3 first, then Arma" order instead of firing both almost simultaneously.
            MessageBox.Show(
                "TeamSpeak hat den Plugin-Installationsdialog geöffnet. Bitte bestätige die Installation " +
                "(und starte TeamSpeak danach einmal neu, falls es bereits offen war), dann auf OK klicken " +
                "um mit dem Start fortzufahren.",
                "Task Force Radio Plugin installieren",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        if (_settings.MuteTeamSpeakSounds)
        {
            var muted = TeamSpeakService.TryMuteNotificationSounds();
            Log(muted
                ? "Benachrichtigungstöne stummgeschaltet."
                : "Konnte Töne nicht automatisch stummschalten - bitte in TS3 unter Optionen > Benachrichtigungen den Soundpack 'Sounds deactivated' auswählen.");
        }

        if (_serverData is not null && !string.IsNullOrWhiteSpace(_serverData.TeamSpeak.Host))
        {
            // A TeamSpeak connect failure shouldn't take Arma 3 down with it - log and move on.
            try
            {
                TeamSpeakService.LaunchAndConnect(ts3Exe, _serverData.TeamSpeak);
                Log($"Verbinde mit TeamSpeak {_serverData.TeamSpeak.Host}:{_serverData.TeamSpeak.Port}...");
            }
            catch (Exception ex)
            {
                Log($"TeamSpeak-Verbindung fehlgeschlagen: {ex.Message}");
            }

            // Give TeamSpeak a head start before Arma 3 launches, instead of both
            // fighting for focus/network at the same instant.
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    private void LaunchArma3()
    {
        if (string.IsNullOrWhiteSpace(_settings.Arma3Path))
        {
            Log("Arma 3 Pfad unbekannt - Start abgebrochen.");
            return;
        }

        var arma3Exe = File.Exists(Path.Combine(_settings.Arma3Path, "arma3_x64.exe"))
            ? Path.Combine(_settings.Arma3Path, "arma3_x64.exe")
            : Path.Combine(_settings.Arma3Path, "arma3.exe");

        var modPaths = _workshopContentPath is not null
            ? ModManager.GetInstalledModPaths(Mods.Select(m => m.Mod), _workshopContentPath)
            : Array.Empty<string>();

        Log($"Starte mit {modPaths.Count} Mods: {string.Join(", ", Mods.Select(m => m.Name))}");

        var process = GameLauncherService.Launch(arma3Exe, modPaths, _serverData?.Arma3);
        Log("Arma 3 wird gestartet...");
        StatusText = "Arma 3 gestartet.";

        if (process is not null)
        {
            IsArma3Running = true;
            _ = MonitorArma3ExitAsync(process);
        }
    }

    private async Task MonitorArma3ExitAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync();
        }
        catch
        {
            // process handle can be unusable in edge cases (e.g. it already exited) - nothing to recover here
        }

        IsArma3Running = false;
        Log("Arma 3 wurde beendet - schließe TeamSpeak...");
        var closed = await TeamSpeakService.CloseAllInstancesAsync();
        Log(closed ? "TeamSpeak wurde geschlossen." : "TeamSpeak war nicht mehr offen.");
    }

    private async Task CheckForUpdatesAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.GithubRepoUrl)) return;

        var updateService = new UpdateService(_settings.GithubRepoUrl);
        await updateService.CheckAndApplyAsync(Log);
    }

    private async Task InitializeSteamAsync()
    {
        var initialized = await Task.Run(() => _steamWorkshop.Initialize());
        SteamUserName = initialized ? _steamWorkshop.GetPersonaName() : null;
        SteamAvatar = initialized ? _steamWorkshop.GetAvatarImage() : null;
    }

    private async Task ServerStatusLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_serverData is not null)
            {
                if (!string.IsNullOrWhiteSpace(_serverData.Arma3.Ip))
                {
                    Arma3ServerOnline = await ServerStatusService.CheckArma3Async(
                        _serverData.Arma3.Ip, _serverData.Arma3.Port, TimeSpan.FromSeconds(3), ct);
                }

                if (!string.IsNullOrWhiteSpace(_serverData.TeamSpeak.Host))
                {
                    TeamSpeakServerOnline = await ServerStatusService.CheckTeamSpeakAsync(
                        _serverData.TeamSpeak.Host, TimeSpan.FromSeconds(3), ct);
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_serverData is null ? 5 : 60), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        _settings = settings;
        _settingsService.Save(_settings);
        DetectPaths();
    }

    private void Log(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            LogText = LogText.Length == 0 ? line : LogText + Environment.NewLine + line;
            StatusText = message;
        });
    }

    public void Dispose()
    {
        _serverStatusCts.Cancel();
        _serverStatusCts.Dispose();
        _steamWorkshop.Dispose();
        _http.Dispose();
    }
}
