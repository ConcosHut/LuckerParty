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
    private readonly TextBlock _playStatus = PartyRoom.Text("", 13, Brushes.White);
    private readonly Border _playUpdateFill = new()
    {
        Name = "ActionProgressFill", Background = PartyRoom.Brush("#195943"),
        HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false, IsVisible = false
    };
    private readonly LinearGradientBrush _homeBackground = new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = new GradientStops
        {
            new GradientStop(Color.Parse("#FFFDF8"), 0), new GradientStop(Color.Parse("#FFFDF8"), .22),
            new GradientStop(Color.Parse("#F1E7D7"), 1)
        }
    };
    private readonly Image _arena = new() { Name = "HeroArena", Stretch = Stretch.Uniform, IsHitTestVisible = false };
    private readonly BrandLogo _headerLogo = new("logo") { Name = "MainBrand", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Control _headerLockup = Wordmark(48);
    private readonly Control _updateCheck = new UpdateCheckMark { Name = "UpdateCheck", Width = 14, Height = 14, IsVisible = false };
    private readonly Grid _playSections = new() { ColumnDefinitions = new("*,72") };
    private readonly Border _playSplit = new()
    {
        Name = "PlaySplitButton", Background = PartyRoom.Coral, CornerRadius = new CornerRadius(22),
        Transitions = new Transitions { new BoxShadowsTransition { Property = Border.BoxShadowProperty, Duration = TimeSpan.FromMilliseconds(120) } }
    };
    private readonly Border _playEdge = new() { CornerRadius = new CornerRadius(22), BorderThickness = new Thickness(1), IsHitTestVisible = false };
    private readonly LauncherIcon _playIcon = new("play") { Width = 24, Height = 24, Foreground = Brushes.White };
    private readonly StackPanel _launchActions = new() { Spacing = 9, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _actionRow = new();
    private readonly Border _footerSurface = new() { Background = Brushes.Transparent };
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
        var header = new Grid { Height = 84 };
        _headerLogo.Margin = new Thickness(28, -4, 0, 0);
        Place(header, _headerLogo);
        _headerLockup.HorizontalAlignment = HorizontalAlignment.Left;
        _headerLockup.VerticalAlignment = VerticalAlignment.Top;
        _headerLockup.Margin = new Thickness(28, 0, 0, 0);
        Place(header, _headerLockup);
        var channel = new StackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 3, 24, 5) };
        channel.Children.Add(ChannelSelector());
        var version = PartyRoom.Text(_controller.Version, 12, PartyRoom.Muted);
        version.TextAlignment = TextAlignment.Right; version.Margin = new Thickness(0, 2, 8, 0);
        channel.Children.Add(version);
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 8, 0) };
        _updateCheck.VerticalAlignment = VerticalAlignment.Center;
        status.Children.Add(_updateCheck); status.Children.Add(_updateStatus);
        channel.Children.Add(status); Place(header, channel);
        return header;
    }

    private Control BuildHome()
    {
        using var stream = AssetLoader.Open(new Uri("avares://LuckerParty.Launcher/Assets/party-room-arena.png"));
        _arena.Source = new Bitmap(stream);
        RenderOptions.SetBitmapInterpolationMode(_arena, BitmapInterpolationMode.HighQuality);
        // White in the approved art becomes the warm backdrop; the source PNG
        // and every regional correction remain untouched.
        _arena.BlendMode = BitmapBlendingMode.Multiply;
        _arena.VerticalAlignment = VerticalAlignment.Bottom;
        _arena.HorizontalAlignment = HorizontalAlignment.Center;
        return new Grid { Children = { _arena, new HeroEdgeBlend(_arena, _mainSurface) { IsHitTestVisible = false } } };
    }

    private void UpdateHomeLayout()
    {
        var width = (Bounds.Width > 0 ? Bounds.Width : Width) - (_controller.AllowMultipleInstances ? 304 : 0);
        var height = Bounds.Height > 0 ? Bounds.Height : Height;
        var settings = _settings?.IsVisible == true;
        var compact = height < 780 || width < 1000;
        _headerLogo.IsVisible = !settings && !_controller.AllowMultipleInstances;
        _headerLockup.IsVisible = !_headerLogo.IsVisible;
        _headerLogo.Width = _headerLogo.Height = compact ? 180 : 228;
        _headerLockup.Width = width < 680 ? 220 : 307;
        _headerLockup.Height = width < 680 ? 60 : 79;
        _arena.Margin = new Thickness(0, compact ? 14 : 0, 0, 0);
        var playHeight = compact ? 76d : 88d;
        _playSplit.Width = Math.Max(0, width - 56);
        _playSplit.Height = _playSections.Height = _play.Height = _more.Height = playHeight;
        _launchActions.Width = _playSplit.Width;
        _more.Width = playHeight;
        _more.Padding = new Thickness((playHeight - 24) / 2);
        _playSections.ColumnDefinitions[1].Width = new GridLength(_more.IsVisible ? playHeight : 0);
        _playUpdateFill.Width = (_playSplit.Width - (_more.IsVisible ? playHeight : 0)) * _downloadProgress / 100;
        _playLabel.FontSize = compact ? 28 : 32;
        _mainSurface.Background = settings ? PartyRoom.Ivory : _homeBackground;
        _footerSurface.IsVisible = _launchActions.IsVisible = !settings;
        _actionRow.Margin = new Thickness(28, 16, 28, 12);
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
            Color = Color.Parse(_controller.UpdateAvailable ? hover ? "#42278568" : "#2A278568" : hover ? "#42DB5263" : "#2ADB5263")
        });
    }

    private Control BuildSettings()
    {
        var content = new StackPanel { Spacing = 20, Margin = new Thickness(28, 20, 28, 16) };
        content.Children.Add(PartyRoom.Heading("Settings", 40));
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
        var playContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        playContent.Children.Add(_playIcon);
        playContent.Children.Add(_playLabel);
        _playStatus.Name = "ActionStatus"; _playStatus.TextAlignment = Avalonia.Media.TextAlignment.Center;
        var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        text.Children.Add(playContent); text.Children.Add(_playStatus);
        var content = new Grid(); content.Children.Add(_playUpdateFill); content.Children.Add(text);
        _play.Content = content; _play.Padding = default;
        _play.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _play.VerticalContentAlignment = VerticalAlignment.Stretch;
        _play.HorizontalAlignment = _more.HorizontalAlignment = HorizontalAlignment.Stretch;
        _play.VerticalAlignment = _more.VerticalAlignment = VerticalAlignment.Stretch;
        _play.CornerRadius = _more.CornerRadius = new CornerRadius(0);
        _more.BorderThickness = new Thickness(1, 0, 0, 0); _more.BorderBrush = PartyRoom.Brush("#FFB7BD");
        Place(_playSections, _play); Place(_playSections, _more, column: 1);
        var face = new Grid();
        Place(face, new Border { Child = _playSections, CornerRadius = new CornerRadius(22), ClipToBounds = true });
        // One shared edge/highlight avoids a seam or unequal rounding between halves.
        _playEdge.Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops { new GradientStop(Color.Parse("#16FFFFFF"), 0), new GradientStop(Colors.Transparent, .55) }
            };
        Place(face, _playEdge);
        _playSplit.Child = face;
        _playSplit.PointerEntered += (_, _) => UpdatePlayShadow();
        _playSplit.PointerExited += (_, _) => UpdatePlayShadow();
        foreach (var button in new[] { _play, _more }) button.PropertyChanged += (_, change) =>
        {
            if (change.Property == Avalonia.Controls.Button.IsPressedProperty || change.Property == IsEnabledProperty) UpdatePlayShadow();
        };
        _launchActions.Children.Add(_playSplit);
        _message.TextAlignment = TextAlignment.Right;
        _launchActions.Children.Add(_message);
        Place(_actionRow, _launchActions);
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
