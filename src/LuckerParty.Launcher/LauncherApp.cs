using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace LuckerParty.Launcher;

internal sealed class LauncherApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new LauncherWindow(new(Program.Options));
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class LauncherWindow : Window
{
    private readonly LauncherController _controller;
    private readonly TextBlock _status = new() { Text = "Getting ready…", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _notes = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 100, Height = 6 };
    private readonly ComboBox _channel;
    private readonly Button _retry = new() { Content = "Retry", IsEnabled = false };
    private readonly Button _play = new() { Content = "Play installed version", IsEnabled = false };
    private readonly CheckBox _multipleInstances;
    private bool _restoringSetting;

    public LauncherWindow(LauncherController controller)
    {
        _controller = controller;
        Title = "Lucker Party";
        Width = 520;
        Height = 510;
        MinWidth = 460;
        MinHeight = 460;
        _channel = new() { ItemsSource = new[] { "Stable", "Beta" }, SelectedIndex = controller.Channel == "beta" ? 1 : 0, Width = 140 };
        _multipleInstances = new()
        {
            Content = "Allow multiple game instances",
            IsChecked = controller.AllowMultipleInstances
        };
        var layout = new StackPanel
        {
            Margin = new Thickness(28), Spacing = 16,
            Children =
            {
                new TextBlock { Text = "LUCKER PARTY", FontSize = 26, FontWeight = FontWeight.Bold },
                new TextBlock { Text = $"Installed version: {controller.Version}" },
                _channel, _status, _progress,
                new ScrollViewer { MaxHeight = 65, Content = _notes },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _retry, _play } },
                new Expander
                {
                    Header = "Settings", IsExpanded = true,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = new StackPanel
                    {
                        Spacing = 8, Children =
                        {
                            _multipleInstances,
                            new TextBlock
                            {
                                Text = "Play again to test multiplayer locally. Updates and channel changes wait until all game instances close.",
                                TextWrapping = TextWrapping.Wrap
                            }
                        }
                    }
                }
            }
        };
        Content = new ScrollViewer { Content = layout };
        controller.Changed += state => Dispatcher.UIThread.Post(() =>
        {
            const string errorPrefix = "LAUNCHER_ERROR: ";
            _status.Text = state.Message.StartsWith(errorPrefix, StringComparison.Ordinal)
                ? state.Message[errorPrefix.Length..] : state.Message;
            if (state.Message.StartsWith("GAME_EXITED", StringComparison.Ordinal) && state.Message.EndsWith("code=0", StringComparison.Ordinal))
                _status.Text = "Game closed. Choose a channel or play again.";
            if (state.Running) _status.Text = controller.RunningGames == 1
                ? "1 game instance is running." : $"{controller.RunningGames} game instances are running.";
            _progress.Value = state.Progress;
            _notes.Text = state.Notes;
            if (state.Message.StartsWith("GAME_STARTED", StringComparison.Ordinal) && controller.GameRunning && !controller.AllowMultipleInstances)
                WindowState = WindowState.Minimized;
            if (state.Message.StartsWith("GAME_EXITED", StringComparison.Ordinal) && !controller.GameRunning)
            { WindowState = WindowState.Normal; Show(); }
            RefreshControls();
        });
        _retry.Click += async (_, _) => await LaunchAsync();
        _play.Click += async (_, _) => await LaunchAsync(playInstalled: true);
        _multipleInstances.IsCheckedChanged += (_, _) =>
        {
            if (_restoringSetting) return;
            try { controller.SetAllowMultipleInstances(_multipleInstances.IsChecked == true); }
            catch (Exception error)
            {
                _status.Text = error.Message;
                _restoringSetting = true;
                _multipleInstances.SetCurrentValue(CheckBox.IsCheckedProperty, controller.AllowMultipleInstances);
                _restoringSetting = false;
            }
            RefreshControls();
        };
        _channel.SelectionChanged += async (_, _) =>
        {
            if (!IsVisible || controller.Busy) return;
            controller.SelectChannel(_channel.SelectedIndex == 1 ? "beta" : "stable");
            await LaunchAsync();
        };
        Opened += async (_, _) => await LaunchAsync();
        Closing += (_, args) =>
        {
            if (controller.Busy || controller.GameRunning)
            {
                args.Cancel = true;
                if (controller.GameRunning) Hide();
            }
        };
    }

    private async Task LaunchAsync(bool playInstalled = false)
    {
        var launch = _controller.RunAsync(playInstalled);
        RefreshControls();
        var result = await launch;
        if (result == 0 && _controller.CheckOnly) { Close(); return; }
        if (result == 0)
        {
            if (!_controller.GameRunning) _status.Text = "Game closed. Choose a channel or play again.";
            _retry.Content = "Play";
        }
        else _retry.Content = "Retry";
        if (!_controller.GameRunning || _controller.AllowMultipleInstances)
        { WindowState = WindowState.Normal; Show(); }
        RefreshControls();
    }

    private void RefreshControls()
    {
        var canPlay = !_controller.Busy && (!_controller.GameRunning || _controller.AllowMultipleInstances);
        _retry.IsEnabled = _play.IsEnabled = canPlay;
        _channel.IsEnabled = !_controller.Busy && !_controller.GameRunning;
        _multipleInstances.IsEnabled = !_controller.Busy;
        if (_controller.GameRunning && _controller.AllowMultipleInstances) _retry.Content = "Play another instance";
        else if (Equals(_retry.Content, "Play another instance")) _retry.Content = "Play";
    }
}
