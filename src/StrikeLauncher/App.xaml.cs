using System.Windows;
using Velopack;

namespace StrikeLauncher;

public partial class App : Application
{
    public App()
    {
        // Must run before any other startup logic (Velopack install/update hooks).
        VelopackApp.Build().Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"Unerwarteter Fehler: {args.ExceptionObject}",
                "Strike Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };
    }
}
