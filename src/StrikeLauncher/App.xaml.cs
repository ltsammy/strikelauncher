using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Velopack;

namespace StrikeLauncher;

public partial class App : Application
{
    public App()
    {
        // Must run before any other startup logic (Velopack install/update hooks).
        VelopackApp.Build().Run();

        // WPF's hardware-accelerated path renders through a Direct3D9Ex swap chain -
        // exactly the kind of surface Steam's overlay hooks into on any process with a
        // live Steam session (there's no official way to opt a window out of the
        // overlay). Forcing software rendering means there's no swap chain for the
        // overlay to attach to at all, instead of just shortening how long it has to
        // find one. A launcher UI is cheap enough to render that the software-rendering
        // cost is not noticeable.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
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
