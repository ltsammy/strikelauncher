namespace StrikeLauncher.Models;

public sealed class AppSettings
{
    public string? SteamPath { get; set; }

    public string? Arma3Path { get; set; }

    public string? TeamSpeakPath { get; set; }

    public string ModlistUrl { get; set; } = string.Empty;

    public string ServerDataUrl { get; set; } = string.Empty;

    public string Ts3PluginUrl { get; set; } = string.Empty;

    // Matches the actual plugin binary bundled in task_force_radio.ts3_plugin
    // (plugins/TFAR_win64.dll), not the mod's own display name.
    public string Ts3PluginDllHint { get; set; } = "TFAR";

    public bool MuteTeamSpeakSounds { get; set; } = true;

    public string GithubRepoUrl { get; set; } = string.Empty;
}
