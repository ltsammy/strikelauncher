using System.Windows;
using StrikeLauncher.ViewModels;

namespace StrikeLauncher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_viewModel.Settings, _viewModel.BackgroundImageSource) { Owner = this };
        if (settingsWindow.ShowDialog() == true)
        {
            _viewModel.SaveSettings(settingsWindow.ResultSettings);
        }
    }
}
