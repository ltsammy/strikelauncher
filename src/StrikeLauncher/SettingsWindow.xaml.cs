using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using StrikeLauncher.Models;

namespace StrikeLauncher;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _original;

    public AppSettings ResultSettings { get; private set; }

    public SettingsWindow(AppSettings current, ImageSource? backgroundImage = null)
    {
        InitializeComponent();
        _original = current;
        ResultSettings = current;
        BackgroundImageElement.Source = backgroundImage;

        Arma3PathBox.Text = current.Arma3Path ?? string.Empty;
        TeamSpeakPathBox.Text = current.TeamSpeakPath ?? string.Empty;
        NicknameBox.Text = current.PlayerNickname;
        ModlistUrlBox.Text = current.ModlistUrl;
        ServerDataUrlBox.Text = current.ServerDataUrl;
        Ts3PluginUrlBox.Text = current.Ts3PluginUrl;
        GithubRepoUrlBox.Text = current.GithubRepoUrl;
        MuteSoundsCheck.IsChecked = current.MuteTeamSpeakSounds;
    }

    private void OnBrowseArma3Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Arma 3 Installationsordner wählen" };
        if (dialog.ShowDialog() == true) Arma3PathBox.Text = dialog.FolderName;
    }

    private void OnBrowseTeamSpeakClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "TeamSpeak 3 Installationsordner wählen" };
        if (dialog.ShowDialog() == true) TeamSpeakPathBox.Text = dialog.FolderName;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        ResultSettings = new AppSettings
        {
            SteamPath = _original.SteamPath,
            Arma3Path = string.IsNullOrWhiteSpace(Arma3PathBox.Text) ? null : Arma3PathBox.Text,
            TeamSpeakPath = string.IsNullOrWhiteSpace(TeamSpeakPathBox.Text) ? null : TeamSpeakPathBox.Text,
            PlayerNickname = NicknameBox.Text.Trim(),
            ModlistUrl = ModlistUrlBox.Text.Trim(),
            ServerDataUrl = ServerDataUrlBox.Text.Trim(),
            Ts3PluginUrl = Ts3PluginUrlBox.Text.Trim(),
            Ts3PluginDllHint = _original.Ts3PluginDllHint,
            GithubRepoUrl = GithubRepoUrlBox.Text.Trim(),
            MuteTeamSpeakSounds = MuteSoundsCheck.IsChecked ?? true
        };

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnCloseIconClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
