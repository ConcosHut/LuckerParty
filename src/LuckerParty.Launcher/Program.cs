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
            Options = LaunchOptions.Parse(args);
            if (Options.Headless)
            {
                // WinExe may have redirected streams but no attached console.
                // Write UTF-8 bytes without calling SetConsoleOutputCP.
                var encoding = new System.Text.UTF8Encoding(false);
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), encoding) { AutoFlush = true });
                Console.SetError(new StreamWriter(Console.OpenStandardError(), encoding) { AutoFlush = true });
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
