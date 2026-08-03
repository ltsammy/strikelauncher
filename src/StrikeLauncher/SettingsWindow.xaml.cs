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
            // Fixed app configuration, not user-editable - carried over unchanged.
            ModlistUrl = _original.ModlistUrl,
            ServerDataUrl = _original.ServerDataUrl,
            Ts3PluginUrl = _original.Ts3PluginUrl,
            Ts3PluginDllHint = _original.Ts3PluginDllHint,
            GithubRepoUrl = _original.GithubRepoUrl,
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
