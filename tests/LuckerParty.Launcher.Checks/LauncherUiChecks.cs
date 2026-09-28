using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
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
                    var primary = FindButton(window, "Play"); var chevron = FindButton(window, "Play options");
                    var primaryPosition = primary.TranslatePoint(new Point(0, 0), window)!.Value;
                    var chevronPosition = chevron.TranslatePoint(new Point(0, 0), window)!.Value;
                    Require(Math.Abs(primary.Bounds.Height - chevron.Bounds.Height) < .1 && Math.Abs(primaryPosition.Y - chevronPosition.Y) < .1,
                        "Play and chevron have flush top and bottom edges");
                    Require(chevron.Bounds.Width <= chevron.Bounds.Height + 1,
                        "Chevron segment has compact equal padding");
                    window.MouseMove(new Point(primaryPosition.X + primary.Bounds.Width / 2, primaryPosition.Y + primary.Bounds.Height / 2));
                    Dispatcher.UIThread.RunJobs();
                    var playSurface = primary.GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Name == "PART_ContentPresenter");
                    Require(playSurface.Background is ISolidColorBrush hover && hover.Color.R > hover.Color.G + 60 && hover.Color.R > hover.Color.B + 60,
                        "Real pointer hover keeps Play coral instead of Fluent gray");
                    await Save(window, output, "party-room-hover.png");
                    window.MouseMove(new Point(5, 5));
                    var stable = FindButton(window, "Stable release channel");
                    stable.Focus(NavigationMethod.Tab);
                    Dispatcher.UIThread.RunJobs();
                    var focus = stable.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "KeyboardFocus");
                    Require(focus.Height == 2 && focus.Width == 28, "Channel keyboard focus stays inside the shared pill as an underline");
                    var initialVersion = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == controller.Version);
                    Require(initialVersion.TranslatePoint(new Point(0,0), window)!.Value.Y < 180,
                        "Installed version sits beneath the top-right channel selector");
                    Click(FindButton(window, "Settings"));
                    var toggle = window.GetVisualDescendants().OfType<ToggleSwitch>().Single();
                    Require(toggle.IsEffectivelyVisible, "Multiple-instance preference lives in Settings");
                    await Save(window, output, "party-room-settings.png");
                    var toggleTrack = toggle.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "SwitchTrack");
                    Require(toggleTrack.Background is ISolidColorBrush offTrack && offTrack.Color == Color.Parse("#E7DEFF"),
                        "Settings switch uses the lilac off state");
                    toggle.Focus(NavigationMethod.Tab);
                    window.KeyPress(Key.Space, RawInputModifiers.None);
                    window.KeyRelease(Key.Space, RawInputModifiers.None);
                    Dispatcher.UIThread.RunJobs();
                    Require(toggle.IsChecked == true, "Custom Settings switch retains Space-key activation");
                    Require(toggleTrack.Background is ISolidColorBrush onTrack && onTrack.Color == Color.Parse("#FF575E"),
                        "Settings switch uses the coral on state");
                    Require(sidebar.IsVisible && controller.AllowMultipleInstances, "Settings toggle reveals the instance sidebar");
                    window.Width = 1240;
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
                    foreach (var channelButton in new[] { FindButton(window, "Stable release channel"), FindButton(window, "Beta release channel") })
                    {
                        var surface = channelButton.GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Name == "PART_ContentPresenter");
                        Require(surface.Foreground is ISolidColorBrush foreground && channelButton.Foreground is ISolidColorBrush expected && foreground.Color == expected.Color,
                            "Disabled channel labels preserve selected and unselected contrast");
                    }
                    await Save(window, output, "party-room-instances.png");
                    var helper = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PlayHelper");
                    Require(helper.Text == "Opens another game window", "Multi-instance Play explains the additional window");
                    Click(FindButton(window, "Settings"));
                    toggle.IsChecked = false;
                    Require(!FindButton(window, "Running").IsEnabled && !sidebar.IsVisible && controller.RunningGames == 3,
                        "Turning the preference off immediately shows disabled Running without closing games");
                    Require(helper.Text != "Opens another game window", "Additional-instance helper disappears immediately when the toggle is off");
                    Click(window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "← Back to play")));
                    await Save(window, output, "party-room-running.png");
                    Click(FindButton(window, "Settings"));
                    toggle.IsChecked = true;
                    Require(FindButton(window, "Play").IsEnabled && helper.Text == "Opens another game window",
                        "Turning the preference on immediately restores Play and its correct helper");
                    Click(window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "← Back to play")));
                    Click(FindButton(window, "Play options"));
                    Require(FindButton(window, "Play options").ContextMenu!.IsOpen, "Installed-version fallback is behind Play options");
                    await Save(window, output, "party-room-dropdown.png");
                    var menu = FindButton(window, "Play options").ContextMenu!;
                    var menuWindow = TopLevel.GetTopLevel(menu)!;
                    menuWindow.KeyPress(Key.Escape, RawInputModifiers.None);
                    menuWindow.KeyRelease(Key.Escape, RawInputModifiers.None);
                    Dispatcher.UIThread.RunJobs();
                    Require(!menu.IsOpen, "Installed-version menu retains Escape-key dismissal");
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
