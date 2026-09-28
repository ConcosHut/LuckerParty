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

    public LauncherWindow(LauncherController controller)
    {
        _controller = controller;
        Title = "Lucker Party";
        Width = 520;
        Height = 390;
        MinWidth = 460;
        MinHeight = 340;
        _channel = new() { ItemsSource = new[] { "Stable", "Beta" }, SelectedIndex = controller.Channel == "beta" ? 1 : 0, Width = 140 };
        Content = new StackPanel
        {
            Margin = new Thickness(28), Spacing = 16,
            Children =
            {
                new TextBlock { Text = "LUCKER PARTY", FontSize = 26, FontWeight = FontWeight.Bold },
                new TextBlock { Text = $"Installed version: {controller.Version}" },
                _channel, _status, _progress,
                new ScrollViewer { MaxHeight = 65, Content = _notes },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _retry, _play } }
            }
        };
        controller.Changed += state => Dispatcher.UIThread.Post(() =>
        {
            const string errorPrefix = "LAUNCHER_ERROR: ";
            _status.Text = state.Message.StartsWith(errorPrefix, StringComparison.Ordinal)
                ? state.Message[errorPrefix.Length..] : state.Message;
            if (state.Message.StartsWith("GAME_EXITED", StringComparison.Ordinal) && state.Message.EndsWith("code=0", StringComparison.Ordinal))
                _status.Text = "Game closed. Choose a channel or play again.";
            if (state.Running) _status.Text = "Game is running.";
            _progress.Value = state.Progress;
            _notes.Text = state.Notes;
            if (state.Running) WindowState = WindowState.Minimized;
        });
        _retry.Click += async (_, _) => await LaunchAsync();
        _play.Click += async (_, _) => await LaunchAsync(playInstalled: true);
        _channel.SelectionChanged += async (_, _) =>
        {
            if (!IsVisible || controller.Busy) return;
            controller.SelectChannel(_channel.SelectedIndex == 1 ? "beta" : "stable");
            await LaunchAsync();
        };
        Opened += async (_, _) => await LaunchAsync();
        Closing += (_, args) =>
        {
            if (controller.Busy)
            {
                args.Cancel = true;
                if (controller.GameRunning) Hide();
            }
        };
    }

    private async Task LaunchAsync(bool playInstalled = false)
    {
        _retry.IsEnabled = _play.IsEnabled = _channel.IsEnabled = false;
        var result = await _controller.RunAsync(playInstalled);
        if (result == 0 && _controller.CheckOnly) { Close(); return; }
        if (result == 0)
        {
            _status.Text = "Game closed. Choose a channel or play again.";
            _retry.Content = "Play";
        }
        else _retry.Content = "Retry";
        WindowState = WindowState.Normal;
        Show();
        _retry.IsEnabled = _play.IsEnabled = _channel.IsEnabled = true;
    }
}
