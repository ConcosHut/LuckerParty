using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
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

internal sealed partial class LauncherWindow : Window
{
    private readonly LauncherController _controller;
    private readonly Grid _shell = new() { ColumnDefinitions = new("Auto,*"), RowDefinitions = new("40,*") };
    private readonly Grid _mainSurface = new() { RowDefinitions = new("Auto,*,Auto") };
    private readonly StackPanel _instances = new() { Spacing = 12 };
    private readonly Dictionary<int, (Border Card, TextBlock Time)> _cards = new();
    private readonly Border _sidebar;
    private readonly TextBlock _count = PartyRoom.Text("0", 14);
    private readonly TextBlock _message = PartyRoom.Text("Getting ready…", 14, PartyRoom.Muted);
    private readonly TextBlock _updateStatus = new()
    {
        Text = "Getting ready", FontSize = 12, FontFamily = PartyRoom.Body,
        Foreground = PartyRoom.Muted, TextWrapping = TextWrapping.NoWrap
    };
    private double _downloadProgress;
    private readonly TextBlock _playLabel = PartyRoom.Text("Play", 20, Brushes.White);
    private readonly Button _play, _more, _stable, _beta;
    private readonly TranslateTransform _channelThumb = new()
    { Transitions = new Transitions { new DoubleTransition { Property = TranslateTransform.XProperty, Duration = TimeSpan.FromMilliseconds(180) } } };
    private readonly ToggleSwitch _multiple = new() { Content = "Allow multiple game instances" };
    private readonly Control _home, _settings;
    private readonly DispatcherTimer _timer;
    private bool _changingSetting;
    private bool _closed;
    private string _lastStateMessage = "";

    public LauncherWindow(LauncherController controller)
    {
        _controller = controller;
        _message.Name = "PlayHelper";
        _updateStatus.Name = "UpdateStatus";
        Title = "Lucker Party"; Background = PartyRoom.Ivory; Foreground = PartyRoom.Ink;
        FontFamily = PartyRoom.Body; FontSize = 14; RequestedThemeVariant = ThemeVariant.Light;
        Width = controller.AllowMultipleInstances ? 1464 : 1160; Height = 840; MinWidth = 820; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Styles.Add(PartyRoom.ButtonStyles());
        _play = Button("", PartyRoom.Coral, Brushes.White); _play.Classes.Add("primary");
        _more = Button(new LauncherIcon("chevron") { Width = 24, Height = 24, Foreground = Brushes.White }, PartyRoom.Coral, Brushes.White);
        _more.Classes.Add("primary"); _more.Width = 56;
        _play.Name = "PrimaryPlay"; AutomationProperties.SetName(_play, "Play"); AutomationProperties.SetName(_more, "Play options");
        _stable = ChannelButton("Stable", "stable"); _beta = ChannelButton("Beta", "beta");
        _home = BuildHome();
        _home.Name = "HomePage";
        _settings = BuildSettings(); _settings.Name = "SettingsPage"; _settings.IsVisible = false;
        _sidebar = BuildSidebar(); Place(_shell, _sidebar, row: 1);
        var header = BuildHeader(); header.ZIndex = 2; _mainSurface.Children.Add(header);
        var pages = new Grid(); pages.Children.Add(_home); pages.Children.Add(_settings); Place(_mainSurface, pages, row: 1);
        Place(_mainSurface, BuildFooter(), row: 2);
        Place(_shell, _mainSurface, row: 1, column: 1);
        var caption = BuildCaptionRow(); caption.ZIndex = 3; Grid.SetColumnSpan(caption, 2); Place(_shell, caption);
        Content = _shell;
        ConfigureChrome();
        _more.ContextMenu = new ContextMenu
        {
            Placement = PlacementMode.BottomEdgeAlignedRight, VerticalOffset = 6,
            ItemsSource = new[] { new MenuItem { Header = "Play installed version" } }
        };
        PartyRoom.StyleMenu(_more.ContextMenu);
        ((MenuItem)_more.ContextMenu.ItemsSource!.Cast<object>().First()).Click += async (_, _) => await RunAsync(playInstalled: true);
        _more.Click += (_, _) => _more.ContextMenu.Open(_more);
        _play.Click += async (_, _) =>
        {
            // Update is an explicit action. A normal Play click may discover a
            // newer version, but cannot silently install it or launch past it.
            if (_controller.UpdateAvailable) await RunAsync(prepareOnly: true);
            else await RunAsync(prepareOnly: _controller.Activity == LauncherActivity.Error, discoverOnly: true);
        };
        controller.Changed += ControllerChanged;
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => RefreshInstances());
        Opened += async (_, _) =>
        {
            FitWindowToWorkingArea();
            _timer.Start(); Refresh();
            await RunAsync(prepareOnly: true, discoverOnly: !Program.Options.Resume);
        };
        Closing += (_, args) =>
        {
            if (controller.Busy || controller.GameRunning)
            { args.Cancel = true; if (controller.GameRunning) WindowState = WindowState.Minimized; }
            else _timer.Stop();
        };
        Closed += (_, _) =>
        {
            _closed = true;
            controller.Changed -= ControllerChanged;
            _timer.Stop();
        };
    }

    private void SetSettingsVisible(bool visible)
    {
        _settings.IsVisible = visible;
        _home.IsVisible = !visible;
        if (_settingsNavigation is { } navigation)
        {
            if (navigation.IsChecked != visible)
                navigation.SetCurrentValue(ToggleButton.IsCheckedProperty, (bool?)visible);
            ToolTip.SetTip(navigation, visible ? "Close Settings" : "Open launcher settings");
            if (!visible) navigation.Focus();
        }
        UpdateHomeLayout();
    }

    private static Button Button(object content, IBrush? background = null, IBrush? foreground = null)
    {
        var button = new Button
        {
            Content = content, Background = background ?? Brushes.Transparent, Foreground = foreground ?? PartyRoom.Ink,
            Padding = new Thickness(14, 9), HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Classes.Add("flat"); return button;
    }
    private static void Place(Grid grid, Control child, int row = 0, int column = 0)
    { Grid.SetRow(child, row); Grid.SetColumn(child, column); grid.Children.Add(child); }

    private Button ChannelButton(string label, string channel)
    {
        var button = Button(label); button.Height = 38; button.FontSize = 15;
        button.Classes.Add("channel");
        AutomationProperties.SetName(button, $"{label} release channel");
        button.Click += async (_, _) =>
        {
            if (_controller.Channel == channel) return;
            _controller.SelectChannel(channel); Refresh(); await RunAsync(prepareOnly: true, discoverOnly: true);
        };
        return button;
    }
    private Control ChannelSelector()
    {
        var track = new Grid { ColumnDefinitions = new("*,*"), Width = 224 };
        var thumb = new Border
        {
            Background = PartyRoom.Coral, Width = 108, Height = 38, CornerRadius = new CornerRadius(22),
            HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = _channelThumb, IsHitTestVisible = false
        };
        Grid.SetColumnSpan(thumb, 2); track.Children.Add(thumb);
        Place(track, _stable); Place(track, _beta, column: 1);
        return new Border
        {
            Background = PartyRoom.Brush("#F1EDF6"), CornerRadius = new CornerRadius(26),
            BorderBrush = PartyRoom.Brush("#E7DFF1"), BorderThickness = new Thickness(1),
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 5, Color = Color.Parse("#0A332442") }),
            Padding = new Thickness(4), Child = track
        };
    }
    private async Task RunAsync(bool playInstalled = false, bool prepareOnly = false, bool discoverOnly = false)
    {
        var task = _controller.RunAsync(playInstalled, prepareOnly, discoverOnly); Refresh();
        var result = await task;
        if (result == 0 && _controller.CheckOnly) { Close(); return; }
        Refresh();
    }
    private void ControllerChanged(LauncherState state) => Dispatcher.UIThread.Post(() =>
    {
        if (!_closed) StateChanged(state);
    });

    private void StateChanged(LauncherState state)
    {
        // Download percentages describe transfer only, not installation or restart.
        _downloadProgress = state.Activity == LauncherActivity.Downloading ? Math.Clamp(state.Progress, 0, 100) : 0;
        _lastStateMessage = state.Message;
        if (state.Message.StartsWith("GAME_EXITED", StringComparison.Ordinal) && !_controller.GameRunning)
        {
            WindowState = WindowState.Normal; Show();
            if (!_controller.Busy) _ = RunAsync(prepareOnly: true, discoverOnly: true);
        }
        Refresh();
    }
    private void Refresh()
    {
        _sidebar.IsVisible = _controller.AllowMultipleInstances;
        var canPlay = !_controller.Busy && (!_controller.GameRunning || _controller.AllowMultipleInstances);
        _more.IsEnabled = canPlay;
        _more.IsVisible = !_controller.Busy && (_controller.UpdateAvailable || _controller.Activity == LauncherActivity.Error);
        if (!_more.IsVisible) _more.ContextMenu?.Close();
        _play.IsEnabled = canPlay && (!_controller.UpdateAvailable || !_controller.GameRunning);
        _stable.IsEnabled = _beta.IsEnabled = !_controller.Busy && !_controller.GameRunning;
        _multiple.IsEnabled = !_controller.Busy;
        _channelThumb.X = _controller.Channel == "beta" ? 112 : 0;
        _stable.Foreground = _controller.Channel == "stable" ? Brushes.White : PartyRoom.Ink;
        _beta.Foreground = _controller.Channel == "beta" ? Brushes.White : PartyRoom.Ink;
        var singleGameBlocksPlay = _controller.GameRunning && !_controller.AllowMultipleInstances;
        _playLabel.Text = _controller.Busy ? _controller.Activity switch
        {
            LauncherActivity.Checking => "Checking…", LauncherActivity.Downloading => $"Updating · {_downloadProgress:0}%",
            LauncherActivity.Applying => "Installing…", _ => "Starting…"
        } : singleGameBlocksPlay ? "Running"
          : _controller.Activity == LauncherActivity.Error ? "Retry update"
          : _controller.UpdateAvailable ? "Update" : "Play";
        var updateAction = _controller.UpdateAvailable;
        _play.Classes.Set("update-action", updateAction);
        _more.Classes.Set("update-action", updateAction);
        var downloading = _controller.Busy && _controller.Activity == LauncherActivity.Downloading;
        var updating = downloading || _controller.Busy && _controller.Activity == LauncherActivity.Applying;
        _play.Classes.Set("updating", updating);
        _playUpdateFill.IsVisible = downloading;
        _playStatus.Text = _controller.Busy ? _controller.Activity switch
        {
            LauncherActivity.Checking => "Checking the selected channel",
            LauncherActivity.Downloading => "Downloading update…",
            LauncherActivity.Applying => "Launcher will restart when ready",
            _ => "Opening game window…"
        } : "";
        _playStatus.IsVisible = _controller.Busy;
        _playIcon.IsVisible = !_controller.Busy;
        _playSplit.Background = updateAction ? PartyRoom.Green : PartyRoom.Coral;
        _playEdge.BorderBrush = PartyRoom.Brush(updateAction ? "#1C6650" : "#CF4F5D");
        _more.BorderBrush = PartyRoom.Brush(updateAction ? "#8ABBAB" : "#FFB7BD");
        _playIcon.Kind = updateAction ? "download" : "play";
        // The same refresh runs after setting changes as after controller events.
        // Helper text therefore cannot retain an obsolete multi-instance hint.
        _message.Text = _controller.Busy ? ""
            : singleGameBlocksPlay ? "Close the game to play again."
            : _controller.Activity == LauncherActivity.Error && _lastStateMessage.StartsWith("LAUNCHER_ERROR: ", StringComparison.Ordinal)
                ? _lastStateMessage[16..] + " Use Play options to launch the installed version."
            : _controller.UpdateAvailable ? _controller.GameRunning ? "Close all game windows to update."
                : $"Install {_controller.AvailableVersion} for {_controller.Channel}."
            : _controller.GameRunning && _controller.AllowMultipleInstances ? "Opens another game window" : "";
        _message.IsVisible = !string.IsNullOrWhiteSpace(_message.Text);
        var channelHint = _controller.GameRunning ? "Close all game windows to change channels."
            : _controller.Busy ? "Wait for the current update or launch to finish." : null;
        ToolTip.SetTip(_stable, channelHint); ToolTip.SetTip(_beta, channelHint);
        AutomationProperties.SetName(_play, _playLabel.Text);
        _updateStatus.Text = _controller.UpdateStatus;
        // Recompute intrinsic width when the status changes after initial layout.
        // Otherwise a longer label can retain the startup label's narrow bounds.
        _updateStatus.InvalidateMeasure();
        if (_updateStatus.Parent is Control statusRow) statusRow.InvalidateMeasure();
        _updateStatus.Foreground = _controller.UpdateStatus == "Up to date" ? PartyRoom.Green : PartyRoom.Muted;
        _updateCheck.IsVisible = _controller.UpdateStatus == "Up to date";
        UpdateHomeLayout();
        RefreshInstances();
    }
}
