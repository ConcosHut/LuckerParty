using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace LuckerParty.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly TextBlock _headline = PartyRoom.Heading("Ready for another round?", 28);
    private readonly TextBlock _subtitle = PartyRoom.Text("New chaos. Same crew.", 17, PartyRoom.Muted);
    private readonly StackPanel _headlineContent = new() { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
    private readonly Image _arena = new() { Name = "HeroArena", Stretch = Stretch.Uniform, IsHitTestVisible = false };
    private readonly BrandLogo _headerLogo = new("logo") { Name = "MainBrand", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Control _headerLockup = Wordmark(48);
    private readonly Control _updateCheck = new UpdateCheckMark { Width = 14, Height = 14, IsVisible = false };
    private readonly Grid _playSections = new() { ColumnDefinitions = new("*,72") };
    private readonly Border _playSplit = new()
    {
        Name = "PlaySplitButton", Background = PartyRoom.Coral, CornerRadius = new CornerRadius(22),
        Transitions = new Transitions { new BoxShadowsTransition { Property = Border.BoxShadowProperty, Duration = TimeSpan.FromMilliseconds(120) } }
    };
    private readonly StackPanel _launchActions = new() { Spacing = 9, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _actionRow = new() { ColumnDefinitions = new("Auto,*,Auto"), RowDefinitions = new("Auto,Auto,Auto"), ColumnSpacing = 24 };
    private readonly Border _footerSurface = new() { Background = PartyRoom.Sand };
    private readonly ToggleButton _settingsNavigation = new() { Name = "SettingsNavigation" };

    private static Control Wordmark(double size)
    {
        var mark = new Grid { ColumnDefinitions = new("Auto,*"), ColumnSpacing = size * .3, Width = size * 6.4, Height = size * 1.65 };
        Place(mark, new BrandLogo("emblem") { Width = size * 2.2, Height = size * 1.65 });
        Place(mark, new BrandLogo("wordmark") { Height = size * 1.65 }, column: 1);
        return mark;
    }

    private Control BuildHeader()
    {
        // The stacked logo uses the empty upper-left of the isometric hero.
        // Its fixed header row permits the logo to extend into the stage below.
        var header = new Grid { RowDefinitions = new("40,84") };
        Place(header, BuildCaptionRow());
        _headerLogo.Margin = new Thickness(28, -4, 0, 0);
        Place(header, _headerLogo, 1);
        _headerLockup.HorizontalAlignment = HorizontalAlignment.Left;
        _headerLockup.VerticalAlignment = VerticalAlignment.Top;
        _headerLockup.Margin = new Thickness(28, 0, 0, 0);
        Place(header, _headerLockup, 1);
        var channel = new StackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 3, 24, 5) };
        channel.Children.Add(ChannelSelector());
        var version = PartyRoom.Text(_controller.Version, 12, PartyRoom.Muted);
        version.TextAlignment = TextAlignment.Right; version.Margin = new Thickness(0, 2, 8, 0);
        channel.Children.Add(version);
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 8, 0) };
        _updateStatus.FontSize = 12; _updateStatus.TextAlignment = TextAlignment.Right;
        _updateCheck.VerticalAlignment = VerticalAlignment.Center;
        status.Children.Add(_updateCheck); status.Children.Add(_updateStatus);
        channel.Children.Add(status); Place(header, channel, 1);
        return header;
    }

    private Control BuildHome()
    {
        using var stream = AssetLoader.Open(new Uri("avares://LuckerParty.Launcher/Assets/party-room-arena.png"));
        _arena.Source = new Bitmap(stream);
        RenderOptions.SetBitmapInterpolationMode(_arena, BitmapInterpolationMode.HighQuality);
        _arena.VerticalAlignment = VerticalAlignment.Bottom;
        _arena.HorizontalAlignment = HorizontalAlignment.Center;
        return new Grid { Margin = new Thickness(22, 0, 22, 0), Children = { _arena } };
    }

    private void UpdateHomeLayout()
    {
        var width = (Bounds.Width > 0 ? Bounds.Width : Width) - (_controller.AllowMultipleInstances ? 304 : 0);
        var height = Bounds.Height > 0 ? Bounds.Height : Height;
        var settings = _settings?.IsVisible == true;
        var compact = height < 780 || width < 1000;
        var stacked = width < 680;
        var secondarySettingsRow = width < 960;
        _headerLogo.IsVisible = !settings && !_controller.AllowMultipleInstances;
        _headerLockup.IsVisible = !_headerLogo.IsVisible;
        _headerLogo.Width = _headerLogo.Height = compact ? 180 : 228;
        _headerLockup.Width = width < 680 ? 220 : 307;
        _headerLockup.Height = width < 680 ? 60 : 79;
        _headline.FontSize = compact ? 24 : 28;
        _subtitle.FontSize = compact ? 15 : 17;
        _arena.Margin = new Thickness(0, compact ? 14 : 0, 0, 0);
        var playHeight = compact ? 64d : 76d;
        _playSplit.Width = Math.Min(compact ? 400 : 440, Math.Max(0, width - 56));
        _playSplit.Height = _playSections.Height = _play.Height = _more.Height = playHeight;
        _launchActions.Width = _playSplit.Width;
        _more.Width = playHeight;
        _more.Padding = new Thickness((playHeight - 24) / 2);
        _playSections.ColumnDefinitions[1].Width = new GridLength(playHeight);
        _playLabel.FontSize = compact ? 24 : 28;
        _footerSurface.Background = settings ? PartyRoom.Ivory : PartyRoom.Sand;
        _headlineContent.IsVisible = _launchActions.IsVisible = !settings;
        _actionRow.Margin = new Thickness(28, settings ? 10 : 22, 28, 18);
        // Keep the grid's cell count stable during Settings/resize layout passes.
        _actionRow.ColumnDefinitions[0].Width = !settings && secondarySettingsRow ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        _actionRow.ColumnDefinitions[1].Width = !settings && secondarySettingsRow ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        _actionRow.ColumnDefinitions[2].Width = !settings && !secondarySettingsRow ? GridLength.Auto : new GridLength(0);
        _settingsNavigation.Margin = !settings && secondarySettingsRow ? new Thickness(0, 12, 0, 0) : default;
        _launchActions.Margin = !settings && stacked ? new Thickness(0, 12, 0, 0) : default;
        Grid.SetRow(_settingsNavigation, !settings && stacked ? 2 : !settings && secondarySettingsRow ? 1 : 0);
        Grid.SetColumn(_settingsNavigation, 0);
        Grid.SetRow(_headlineContent, 0);
        Grid.SetColumn(_headlineContent, secondarySettingsRow ? 0 : 1);
        Grid.SetColumnSpan(_headlineContent, stacked ? 2 : 1);
        Grid.SetRow(_launchActions, stacked ? 1 : 0);
        Grid.SetColumn(_launchActions, stacked ? 0 : secondarySettingsRow ? 1 : 2);
        Grid.SetColumnSpan(_launchActions, stacked ? 2 : 1);
        _launchActions.HorizontalAlignment = HorizontalAlignment.Right;
        UpdatePlayShadow();
    }

    private void UpdatePlayShadow()
    {
        var pressed = _play.IsPressed || _more.IsPressed;
        var hover = _playSplit.IsPointerOver;
        _playSplit.BoxShadow = !_play.IsEnabled ? default : new BoxShadows(new BoxShadow
        {
            OffsetY = pressed ? 2 : hover ? 9 : 6, Blur = pressed ? 7 : hover ? 24 : 18,
            Color = Color.Parse(hover ? "#42DB5263" : "#2ADB5263")
        });
    }

    private Control BuildSettings()
    {
        var content = new StackPanel { Spacing = 20, Margin = new Thickness(28, 0, 28, 24) };
        var back = Button("← Back to play"); back.HorizontalAlignment = HorizontalAlignment.Left;
        back.Click += (_, _) => SetSettingsVisible(false);
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
        var card = new StackPanel { Spacing = 14 }; card.Children.Add(_multiple);
        card.Children.Add(PartyRoom.Text("Open several game windows to test multiplayer on one PC. The running-instances sidebar appears when this is enabled.", 15, PartyRoom.Muted));
        card.Children.Add(PartyRoom.Text("Turning this off hides the sidebar and prevents new copies. Existing games keep running.", 13, PartyRoom.Muted));
        content.Children.Add(new Border
        {
            Background = PartyRoom.Card, BorderBrush = PartyRoom.Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20), Padding = new Thickness(24), Child = card
        });
        return new ScrollViewer { Content = content };
    }

    private Control BuildFooter()
    {
        _headlineContent.Children.Add(_headline); _headlineContent.Children.Add(_subtitle);
        var playContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        playContent.Children.Add(new LauncherIcon("play") { Width = 24, Height = 24, Foreground = Brushes.White });
        playContent.Children.Add(_playLabel); _play.Content = playContent;
        _play.HorizontalAlignment = _more.HorizontalAlignment = HorizontalAlignment.Stretch;
        _play.VerticalAlignment = _more.VerticalAlignment = VerticalAlignment.Stretch;
        _play.CornerRadius = _more.CornerRadius = new CornerRadius(0);
        _more.BorderThickness = new Thickness(1, 0, 0, 0); _more.BorderBrush = PartyRoom.Brush("#FFB7BD");
        Place(_playSections, _play); Place(_playSections, _more, column: 1);
        var face = new Grid();
        Place(face, new Border { Child = _playSections, CornerRadius = new CornerRadius(22), ClipToBounds = true });
        // One shared edge/highlight avoids a seam or unequal rounding between halves.
        Place(face, new Border
        {
            CornerRadius = new CornerRadius(22), BorderThickness = new Thickness(1), BorderBrush = PartyRoom.Brush("#CF4F5D"), IsHitTestVisible = false,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops { new GradientStop(Color.Parse("#16FFFFFF"), 0), new GradientStop(Colors.Transparent, .55) }
            }
        });
        _playSplit.Child = face;
        _playSplit.PointerEntered += (_, _) => UpdatePlayShadow();
        _playSplit.PointerExited += (_, _) => UpdatePlayShadow();
        foreach (var button in new[] { _play, _more }) button.PropertyChanged += (_, change) =>
        {
            if (change.Property == Avalonia.Controls.Button.IsPressedProperty || change.Property == IsEnabledProperty) UpdatePlayShadow();
        };
        _launchActions.Children.Add(_playSplit);
        _message.TextAlignment = TextAlignment.Right;
        _launchActions.Children.Add(_message); _launchActions.Children.Add(_progress);
        var settingsContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        settingsContent.Children.Add(new LauncherIcon("gear") { Width = 24, Height = 24 });
        var label = PartyRoom.Text("Settings", 17); label.FontWeight = FontWeight.Bold; settingsContent.Children.Add(label);
        _settingsNavigation.Content = settingsContent;
        _settingsNavigation.HorizontalAlignment = HorizontalAlignment.Left;
        _settingsNavigation.VerticalAlignment = VerticalAlignment.Center;
        _settingsNavigation.HorizontalContentAlignment = HorizontalAlignment.Center;
        _settingsNavigation.VerticalContentAlignment = VerticalAlignment.Center;
        _settingsNavigation.Padding = new Thickness(18, 12);
        _settingsNavigation.Height = 50; _settingsNavigation.MinWidth = 154;
        _settingsNavigation.CornerRadius = new CornerRadius(16); _settingsNavigation.Classes.Add("settings-nav");
        Avalonia.Automation.AutomationProperties.SetName(_settingsNavigation, "Settings");
        _settingsNavigation.IsCheckedChanged += (_, _) => SetSettingsVisible(_settingsNavigation.IsChecked == true);
        Place(_actionRow, _settingsNavigation); Place(_actionRow, _headlineContent); Place(_actionRow, _launchActions);
        _footerSurface.Child = _actionRow;
        UpdateHomeLayout();
        return _footerSurface;
    }
}

internal sealed class UpdateCheckMark : Control
{
    public override void Render(DrawingContext context)
    {
        using var scale = context.PushTransform(Matrix.CreateScale(Bounds.Width / 14, Bounds.Height / 14));
        context.DrawEllipse(PartyRoom.Green, null, new Point(7, 7), 7, 7);
        context.DrawGeometry(null, new Pen(Brushes.White, 1.5), Geometry.Parse("M 3.5,7 L 6,9.5 L 10.5,4.5"));
    }
}
