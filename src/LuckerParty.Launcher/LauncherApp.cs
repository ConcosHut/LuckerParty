using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

internal sealed class LauncherWindow : Window
{
    private readonly LauncherController _controller;
    private readonly Grid _shell = new() { ColumnDefinitions = new("Auto,*") };
    private readonly StackPanel _instances = new() { Spacing = 12 };
    private readonly Dictionary<int, (Border Card, TextBlock Time)> _cards = new();
    private readonly Border _sidebar;
    private readonly TextBlock _count = PartyRoom.Text("0", 14);
    private readonly TextBlock _message = PartyRoom.Text("Getting ready…", 14, PartyRoom.Muted);
    private readonly TextBlock _updateStatus = PartyRoom.Text("Getting ready", 13, PartyRoom.Muted);
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 100, Height = 5, IsVisible = false };
    private readonly TextBlock _playLabel = PartyRoom.Text("Play", 20, Brushes.White);
    private readonly Button _play, _more, _stable, _beta;
    private readonly TranslateTransform _channelThumb = new()
    { Transitions = new Transitions { new DoubleTransition { Property = TranslateTransform.XProperty, Duration = TimeSpan.FromMilliseconds(180) } } };
    private readonly ToggleSwitch _multiple = new() { Content = "Allow multiple game instances" };
    private readonly Control _home, _settings;
    private readonly DispatcherTimer _timer;
    private bool _changingSetting;

    public LauncherWindow(LauncherController controller)
    {
        _controller = controller;
        Title = "Lucker Party"; Background = PartyRoom.Ivory; Foreground = PartyRoom.Ink;
        FontFamily = PartyRoom.Body; FontSize = 14; RequestedThemeVariant = ThemeVariant.Light;
        Width = controller.AllowMultipleInstances ? 1240 : 1000; Height = 840; MinWidth = 820; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Styles.Add(PartyRoom.ButtonStyles());
        _play = Button("", PartyRoom.Coral, Brushes.White); _play.Classes.Add("primary");
        _more = Button(new LauncherIcon("chevron") { Width = 24, Height = 24, Foreground = Brushes.White }, PartyRoom.Coral, Brushes.White);
        _more.Classes.Add("primary"); _more.Width = 56;
        AutomationProperties.SetName(_play, "Play"); AutomationProperties.SetName(_more, "Play options");
        _stable = ChannelButton("Stable", "stable"); _beta = ChannelButton("Beta", "beta");
        _home = BuildHome();
        _settings = BuildSettings(); _settings.IsVisible = false;
        _sidebar = BuildSidebar(); _shell.Children.Add(_sidebar);
        var main = new Grid { RowDefinitions = new("*,Auto") };
        var pages = new Grid(); pages.Children.Add(_home); pages.Children.Add(_settings); main.Children.Add(pages);
        var footer = BuildFooter(); Grid.SetRow(footer, 1); main.Children.Add(footer);
        Grid.SetColumn(main, 1); _shell.Children.Add(main);
        Content = _shell;
        _more.ContextMenu = new ContextMenu { ItemsSource = new[] { new MenuItem { Header = "Play installed version" } } };
        ((MenuItem)_more.ContextMenu.ItemsSource!.Cast<object>().First()).Click += async (_, _) => await RunAsync(playInstalled: true);
        _more.Click += (_, _) => _more.ContextMenu.Open(_more);
        _play.Click += async (_, _) => await RunAsync();
        controller.Changed += state => Dispatcher.UIThread.Post(() => StateChanged(state));
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => RefreshInstances());
        Opened += async (_, _) =>
        {
            if (Screens.ScreenFromWindow(this) is { } screen)
            {
                var available = screen.WorkingArea.Size.ToSize(1 / screen.Scaling);
                Height = Math.Min(Height, available.Height - 32); Width = Math.Min(Width, available.Width - 32);
            }
            _timer.Start(); Refresh();
            await RunAsync(prepareOnly: !Program.Options.Resume);
        };
        Closing += (_, args) =>
        {
            if (controller.Busy || controller.GameRunning)
            { args.Cancel = true; if (controller.GameRunning) WindowState = WindowState.Minimized; }
            else _timer.Stop();
        };
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
        button.Resources["ButtonBackgroundDisabled"] = Brushes.Transparent;
        button.Resources["ButtonForegroundDisabled"] = PartyRoom.Muted;
        AutomationProperties.SetName(button, $"{label} release channel");
        button.Click += async (_, _) =>
        {
            if (_controller.Channel == channel) return;
            _controller.SelectChannel(channel); Refresh(); await RunAsync(prepareOnly: true);
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
        return new Border { Background = PartyRoom.Brush("#F1EDF6"), CornerRadius = new CornerRadius(26), Padding = new Thickness(4), Child = track };
    }
    private static Control Wordmark(double fontSize)
    {
        var title = PartyRoom.Heading("LUCKER\nPARTY", fontSize); title.LineHeight = fontSize * .94;
        title.TextAlignment = TextAlignment.Center; return title;
    }

    private Control BuildHome()
    {
        var home = new Grid { RowDefinitions = new("Auto,Auto,*,Auto,Auto,Auto"), Margin = new Thickness(32, 20, 32, 14) };
        var channel = ChannelSelector(); channel.HorizontalAlignment = HorizontalAlignment.Right; Place(home, channel);
        var title = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        var tagline = PartyRoom.Text("A SMALL ARENA. A BIGGER PARTY.", 12); tagline.LetterSpacing = 1.5;
        tagline.HorizontalAlignment = HorizontalAlignment.Center; title.Children.Add(tagline); title.Children.Add(Wordmark(62));
        Place(home, title, 1);
        using var stream = AssetLoader.Open(new Uri("avares://LuckerParty.Launcher/Assets/party-room-arena.png"));
        var art = new Image { Source = new Bitmap(stream), Stretch = Stretch.Uniform, Margin = new Thickness(0, 8), MinHeight = 120 };
        Place(home, art, 2);
        var headline = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 14) };
        var heading = PartyRoom.Heading("Ready for another round?", 34); heading.TextAlignment = TextAlignment.Center;
        var subtitle = PartyRoom.Text("New chaos. Same crew.", 17, PartyRoom.Muted); subtitle.TextAlignment = TextAlignment.Center;
        headline.Children.Add(heading); headline.Children.Add(subtitle); Place(home, headline, 3);
        var action = new Grid { ColumnDefinitions = new("*,Auto"), Width = 310, Height = 62, HorizontalAlignment = HorizontalAlignment.Center };
        var playContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center };
        playContent.Children.Add(new LauncherIcon("play") { Width = 24, Height = 24, Foreground = Brushes.White });
        playContent.Children.Add(_playLabel); _play.Content = playContent;
        _play.HorizontalAlignment = HorizontalAlignment.Stretch;
        _play.CornerRadius = new CornerRadius(18, 0, 0, 18); _more.CornerRadius = new CornerRadius(0, 18, 18, 0);
        _more.BorderThickness = new Thickness(1, 0, 0, 0); _more.BorderBrush = PartyRoom.Brush("#FFB0B3");
        Place(action, _play); Place(action, _more, column: 1); Place(home, action, 4);
        var details = new StackPanel { Spacing = 6, Margin = new Thickness(0, 10, 0, 0), MaxWidth = 640 };
        _message.TextAlignment = TextAlignment.Center; details.Children.Add(_message); details.Children.Add(_progress); Place(home, details, 5);
        return home;
    }

    private Border BuildSidebar()
    {
        var rail = new Grid { RowDefinitions = new("Auto,Auto,*,Auto"), Margin = new Thickness(20, 28) };
        var wordmark = Wordmark(36); wordmark.HorizontalAlignment = HorizontalAlignment.Left;
        Place(rail, wordmark);
        var header = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new Thickness(0, 26, 0, 18) };
        var text = PartyRoom.Text("Running instances", 18); text.FontWeight = FontWeight.ExtraBold; Place(header, text);
        Place(header, new Border { Background = PartyRoom.Lilac, CornerRadius = new CornerRadius(20), Padding = new Thickness(12, 3), Child = _count }, column: 1);
        Place(rail, header, 1);
        Place(rail, new ScrollViewer { Content = _instances }, 2);
        Place(rail, PartyRoom.Text("Updates wait until all game windows close.", 12, PartyRoom.Muted), 3);
        return new Border { Name = "InstanceSidebar", Width = 280, Background = PartyRoom.Sand, Child = rail };
    }

    private Control BuildSettings()
    {
        var content = new StackPanel { Spacing = 22, Margin = new Thickness(40, 32) };
        var back = Button("← Back to play"); back.HorizontalAlignment = HorizontalAlignment.Left;
        back.Click += (_, _) => { _home.IsVisible = true; _settings.IsVisible = false; };
        content.Children.Add(back); content.Children.Add(PartyRoom.Heading("Settings", 40));
        content.Children.Add(PartyRoom.Text("Make the launcher work your way.", 16, PartyRoom.Muted));
        _multiple.IsChecked = _controller.AllowMultipleInstances; _multiple.FontSize = 16;
        _multiple.IsCheckedChanged += (_, _) =>
        {
            if (_changingSetting) return;
            try { _controller.SetAllowMultipleInstances(_multiple.IsChecked == true); }
            catch (Exception error)
            {
                _message.Text = error.Message; _changingSetting = true;
                _multiple.IsChecked = _controller.AllowMultipleInstances; _changingSetting = false;
            }
            Refresh();
        };
        var card = new StackPanel { Spacing = 14 };
        card.Children.Add(_multiple);
        card.Children.Add(PartyRoom.Text("Open several game windows to test multiplayer on one PC. The running-instances sidebar appears when this is enabled.", 15, PartyRoom.Muted));
        card.Children.Add(PartyRoom.Text("Turning this off hides the sidebar and prevents new copies. Existing games keep running.", 13, PartyRoom.Muted));
        content.Children.Add(new Border { Background = PartyRoom.Card, CornerRadius = new CornerRadius(20), Padding = new Thickness(26), Child = card });
        return new ScrollViewer { Content = content };
    }

    private Control BuildFooter()
    {
        var footer = new Grid { ColumnDefinitions = new("Auto,*,Auto"), Margin = new Thickness(28, 0, 28, 16) };
        var settingsContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        settingsContent.Children.Add(new LauncherIcon("gear") { Width = 24, Height = 24 }); settingsContent.Children.Add(PartyRoom.Text("Settings", 15));
        var settings = Button(settingsContent); AutomationProperties.SetName(settings, "Settings");
        settings.Click += (_, _) => { _home.IsVisible = false; _settings.IsVisible = true; };
        Place(footer, settings);
        var identity = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var version = PartyRoom.Text(_controller.Version, 13, PartyRoom.Muted); version.TextAlignment = TextAlignment.Right;
        _updateStatus.TextAlignment = TextAlignment.Right; identity.Children.Add(version); identity.Children.Add(_updateStatus);
        Place(footer, identity, column: 2);
        return new Border { BorderBrush = PartyRoom.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 12, 0, 0), Child = footer };
    }

    private async Task RunAsync(bool playInstalled = false, bool prepareOnly = false)
    {
        var task = _controller.RunAsync(playInstalled, prepareOnly); Refresh();
        var result = await task;
        if (result == 0 && _controller.CheckOnly) { Close(); return; }
        Refresh();
    }
    private void StateChanged(LauncherState state)
    {
        _progress.Value = state.Progress;
        _progress.IsVisible = state.Activity is LauncherActivity.Downloading or LauncherActivity.Applying;
        if (state.Message.StartsWith("LAUNCHER_ERROR: ", StringComparison.Ordinal))
            _message.Text = state.Message[16..] + " Use Play options to launch the installed version.";
        else if (state.Activity is LauncherActivity.Checking or LauncherActivity.Downloading or LauncherActivity.Applying) _message.Text = state.Message;
        else _message.Text = _controller.GameRunning
            ? _controller.AllowMultipleInstances ? "Opens another game window" : "Game is running. Close it to play again."
            : "";
        if (state.Message.StartsWith("GAME_EXITED", StringComparison.Ordinal) && !_controller.GameRunning)
        {
            WindowState = WindowState.Normal; Show();
            if (!_controller.Busy) _ = RunAsync(prepareOnly: true);
        }
        Refresh();
    }
    private void Refresh()
    {
        _sidebar.IsVisible = _controller.AllowMultipleInstances;
        var canPlay = !_controller.Busy && (!_controller.GameRunning || _controller.AllowMultipleInstances);
        _play.IsEnabled = _more.IsEnabled = canPlay;
        _stable.IsEnabled = _beta.IsEnabled = !_controller.Busy && !_controller.GameRunning;
        _multiple.IsEnabled = !_controller.Busy;
        _channelThumb.X = _controller.Channel == "beta" ? 112 : 0;
        _stable.Foreground = _controller.Channel == "stable" ? Brushes.White : PartyRoom.Ink;
        _beta.Foreground = _controller.Channel == "beta" ? Brushes.White : PartyRoom.Ink;
        _playLabel.Text = _controller.Activity switch
        { LauncherActivity.Checking => "Checking…", LauncherActivity.Downloading => "Updating…", LauncherActivity.Applying => "Applying…", LauncherActivity.Error => "Retry update", _ => "Play" };
        AutomationProperties.SetName(_play, _playLabel.Text);
        _updateStatus.Text = _controller.UpdateStatus;
        _updateStatus.Foreground = _controller.UpdateStatus == "Up to date" ? PartyRoom.Green : PartyRoom.Muted;
        RefreshInstances();
    }
    private void RefreshInstances()
    {
        var games = _controller.Instances; _count.Text = games.Length.ToString();
        var ids = games.Select(game => game.Identity.Pid).ToHashSet();
        foreach (var id in _cards.Keys.Where(id => !ids.Contains(id)).ToArray())
        { _instances.Children.Remove(_cards[id].Card); _cards.Remove(id); }
        if (games.Length == 0)
        {
            if (_instances.Children.Count == 0) _instances.Children.Add(PartyRoom.Text("Your game windows will appear here. Press Play to start one.", 14, PartyRoom.Muted));
            return;
        }
        if (_cards.Count == 0) _instances.Children.Clear();
        foreach (var game in games)
        {
            if (!_cards.TryGetValue(game.Identity.Pid, out var card))
            {
                card = BuildInstanceCard(game); _cards[game.Identity.Pid] = card; _instances.Children.Add(card.Card);
            }
            var minutes = (int)(DateTime.UtcNow - game.StartedAt).TotalMinutes;
            card.Time.Text = game.Closing ? "Closing…" : minutes == 0 ? "Running · just now" : $"Running · {minutes} min";
            if (card.Card.Child is Grid layout && layout.Children.OfType<StackPanel>().FirstOrDefault() is { } details)
            {
                var buttons = details.Children.OfType<StackPanel>().First();
                foreach (var button in buttons.Children.OfType<Button>()) button.IsEnabled = !game.Closing;
                buttons.Children.OfType<Button>().Last().Content = game.CloseFailed ? "Force close" : "Close";
            }
        }
    }
    private (Border Card, TextBlock Time) BuildInstanceCard(GameInstance game)
    {
        var row = new Grid { ColumnDefinitions = new("64,*") };
        Place(row, new CapsuleAvatar(game.Number) { Width = 64, Height = 82 });
        var details = new StackPanel { Spacing = 6, Margin = new Thickness(8, 0, 0, 0) };
        var label = PartyRoom.Text($"Instance {game.Number:00}", 16); label.FontWeight = FontWeight.ExtraBold;
        details.Children.Add(label); var time = PartyRoom.Text("Running · just now", 12, PartyRoom.Muted); details.Children.Add(time);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var show = Button("Show", PartyRoom.Lilac); show.Padding = new Thickness(10, 7);
        show.Click += async (_, _) => await _controller.ShowInstanceAsync(game.Identity);
        var close = Button("Close", PartyRoom.Brush("#ECE9E6")); close.Padding = new Thickness(10, 7);
        close.Click += async (_, _) => await _controller.CloseInstanceAsync(game.Identity,
            force: _controller.Instances.Any(instance => instance.Identity == game.Identity && instance.CloseFailed));
        AutomationProperties.SetName(show, $"Show instance {game.Number}"); AutomationProperties.SetName(close, $"Close instance {game.Number}");
        actions.Children.Add(show); actions.Children.Add(close); details.Children.Add(actions); Place(row, details, column: 1);
        return (new Border { Background = PartyRoom.Card, CornerRadius = new CornerRadius(18), Padding = new Thickness(12, 16), Child = row }, time);
    }
}
