using CommunityToolkit.Mvvm.ComponentModel;

namespace StrikeLauncher.Models;

public enum ModStatus
{
    Missing,
    Installed,
    Subscribing,
    Failed
}

public sealed partial class ModStatusItem : ObservableObject
{
    public ModStatusItem(ModEntry mod)
    {
        Mod = mod;
    }

    public ModEntry Mod { get; }

    public string Name => Mod.Name;

    public ulong WorkshopId => Mod.WorkshopId;

    [ObservableProperty]
    private ModStatus _status;

    partial void OnStatusChanged(ModStatus value) => OnPropertyChanged(nameof(StatusLabel));

    public string StatusLabel => Status switch
    {
        ModStatus.Installed => "Installiert",
        ModStatus.Missing => "Fehlt",
        ModStatus.Subscribing => "Wird geladen...",
        ModStatus.Failed => "Fehlgeschlagen",
        _ => "Unbekannt"
    };
}
