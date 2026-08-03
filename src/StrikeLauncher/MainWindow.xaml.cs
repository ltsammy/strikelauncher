using System.Windows;
using System.Windows.Controls;
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

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    private void OnLogTextChanged(object sender, TextChangedEventArgs e) => LogTextBox.ScrollToEnd();
}
