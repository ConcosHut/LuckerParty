using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LuckerParty.Launcher;

public static class UiTestApplication
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<LauncherApp>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

internal static class LauncherUiChecks
{
    public static async Task Run(string installation, string data)
    {
        Console.WriteLine("UI_CHECK: initialize headless session");
        var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApplication));
        Console.WriteLine("UI_CHECK: dispatch UI scenario");
        try
        {
            await session.Dispatch(async () =>
            {
                Console.WriteLine("UI_CHECK: construct launcher");
                var output = Path.Combine(Environment.CurrentDirectory, "artifacts", "launcher-ui-checks");
                Directory.CreateDirectory(output);
                var controller = new LauncherController(new LaunchOptions { NoUpdate = true }, installation, data);
                var window = new LauncherWindow(controller);
                Console.WriteLine("UI_CHECK: show launcher");
                window.Show();
                try
                {
                    await Wait(() => !controller.Busy && FindButton(window, "Play").IsEnabled);
                    var sidebar = window.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "InstanceSidebar");
                    Require(!sidebar.IsVisible && !controller.GameRunning, "Normal launcher opens without sidebar or an automatic game");
                    Require(window.GetVisualDescendants().OfType<TextBlock>().Count(text => text.Text == controller.Version) == 1,
                        "Installed version is displayed once");
                    await Save(window, output, "party-room-home.png");
                    Click(FindButton(window, "Settings"));
                    var toggle = window.GetVisualDescendants().OfType<ToggleSwitch>().Single();
                    Require(toggle.IsEffectivelyVisible, "Multiple-instance preference lives in Settings");
                    await Save(window, output, "party-room-settings.png");
                    toggle.IsChecked = true;
                    Require(sidebar.IsVisible && controller.AllowMultipleInstances, "Settings toggle reveals the instance sidebar");
                    Click(window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "← Back to play")));
                    Require(!toggle.IsEffectivelyVisible, "Main screen does not expose the multiple-instance toggle");
                    for (var count = 1; count <= 3; count++)
                    {
                        Click(FindButton(window, "Play"));
                        await Wait(() => controller.RunningGames == count && !controller.Busy);
                    }
                    await Wait(() => window.GetVisualDescendants().OfType<Button>().Count(button => AutomationProperties.GetName(button)?.StartsWith("Close instance") == true) == 3);
                    Require(FindButton(window, "Play").IsEnabled, "Play stays enabled with three running games");
                    Require(!FindButton(window, "Stable release channel").IsEnabled, "Channel changes are disabled while games run");
                    await Save(window, output, "party-room-instances.png");
                    Click(FindButton(window, "Play options"));
                    Require(FindButton(window, "Play options").ContextMenu!.IsOpen, "Installed-version fallback is behind Play options");
                    FindButton(window, "Play options").ContextMenu!.Close();
                    Click(FindButton(window, "Show instance 1"));
                    await Wait(() => File.Exists(Path.Combine(installation, "game", $"show-{controller.Instances.First().Identity.Pid}")));
                    Click(FindButton(window, "Close instance 2"));
                    await Wait(() => controller.RunningGames == 2);
                    Require(controller.Instances.All(game => game.Number != 2), "Close removes only the selected instance");
                    foreach (var game in controller.Instances) await controller.CloseInstanceAsync(game.Identity);
                    await Wait(() => !controller.GameRunning && !controller.Busy);
                    Click(FindButton(window, "Settings")); toggle.IsChecked = false;
                    Require(!sidebar.IsVisible, "Disabling multiple instances removes the sidebar column");
                    Click(window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "← Back to play")));
                    window.Width = 850; window.Height = 620;
                    await Save(window, output, "party-room-small.png");
                    var play = FindButton(window, "Play");
                    var position = play.TranslatePoint(new Point(0, 0), window)!.Value;
                    Require(play.IsEffectivelyVisible && position.Y >= 0 && position.Y + play.Bounds.Height <= window.Bounds.Height,
                        "Primary action remains within the minimum window size");
                    var version = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == controller.Version);
                    var versionPosition = version.TranslatePoint(new Point(0, 0), window)!.Value;
                    Require(version.IsEffectivelyVisible && versionPosition.X + version.Bounds.Width <= window.Bounds.Width,
                        "Installed version remains inside the window");
                    Console.WriteLine("LAUNCHER_UI_CHECK_PASS: layouts, fonts/art rendering, Settings, three games, Play menu and targeted Show/Close");
                }
                finally
                {
                    foreach (var game in controller.Instances) await controller.CloseInstanceAsync(game.Identity, force: true);
                    await Wait(() => !controller.GameRunning && !controller.Busy);
                    window.Close();
                }
                return 0;
            }, CancellationToken.None).ConfigureAwait(false);
        }
        // The headless session completes Dispatch on its worker; disposing there
        // would join the same worker. Dispose on a separate pool task.
        finally { await Task.Run(session.Dispose); }
    }

    private static Button FindButton(Window window, string name) => window.GetVisualDescendants().OfType<Button>()
        .Single(button => AutomationProperties.GetName(button) == name);
    private static void Click(Button button)
    { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
    private static async Task Save(Window window, string output, string file)
    {
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();
        foreach (var visual in window.GetVisualDescendants()) visual.InvalidateVisual();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
        using var image = window.CaptureRenderedFrame() ?? throw new Exception("UI rendering failed.");
        image.Save(Path.Combine(output, file));

    }
    private static async Task Wait(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new Exception("Launcher UI timed out.");
            await Task.Delay(30);
        }
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("CHECK_PASS: " + message);
    }
}
