namespace StrikeLauncher.Models;

public sealed class ServerData
{
    public ArmaServerInfo Arma3 { get; set; } = new();

    public TeamSpeakServerInfo TeamSpeak { get; set; } = new();

    public string LauncherBackgroundUrl { get; set; } = string.Empty;
}

public sealed class ArmaServerInfo
{
    public string Ip { get; set; } = string.Empty;

    public int Port { get; set; } = 2302;

    public string Password { get; set; } = string.Empty;
}

public sealed class TeamSpeakServerInfo
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 9987;

    public string Password { get; set; } = string.Empty;
}
