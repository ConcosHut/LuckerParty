using Avalonia;
using Velopack;

namespace LuckerParty.Launcher;

internal static class Program
{
    internal static LaunchOptions Options { get; private set; } = new();

    [STAThread]
    private static int Main(string[] args)
    {
        // Installer/update callbacks must exit here before normal startup.
        // Our session guard, rather than SDK startup, decides when applying is safe.
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        try
        {
            Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            Options = LaunchOptions.Parse(args);
            if (Options.Headless)
            {
                var controller = new LauncherController(Options);
                controller.Changed += state => Console.WriteLine(state.Message);
                return controller.RunAsync().GetAwaiter().GetResult();
            }
            return AppBuilder.Configure<LauncherApp>().UsePlatformDetect()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"LAUNCHER_ERROR: {error.Message}");
            return 1;
        }
    }
}
