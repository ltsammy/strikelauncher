namespace StrikeLauncher.Models;

public sealed class AppSettings
{
    public string? SteamPath { get; set; }

    public string? Arma3Path { get; set; }

    public string? TeamSpeakPath { get; set; }

    public string PlayerNickname { get; set; } = string.Empty;

    public string ModlistUrl { get; set; } = string.Empty;

    public string ServerDataUrl { get; set; } = string.Empty;

    public string Ts3PluginUrl { get; set; } = string.Empty;

    public string Ts3PluginDllHint { get; set; } = "task_force_radio";

    public bool MuteTeamSpeakSounds { get; set; } = true;

    public string GithubRepoUrl { get; set; } = string.Empty;
}
