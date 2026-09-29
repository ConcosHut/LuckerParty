using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace LuckerParty.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly TextBlock _tagline = PartyRoom.Text("A SMALL ARENA. A BIGGER PARTY.", 12);
    private readonly TextBlock _headline = PartyRoom.Heading("Ready for another round?", 40);
    private readonly StackPanel _heroTitle = new() { Spacing = 8 };
    private readonly Grid _heroArea = new() { RowDefinitions = new("Auto,*"), ClipToBounds = true };
    private readonly Image _arena = new() { Stretch = Stretch.Uniform, IsHitTestVisible = false };
    private readonly Control _updateCheck = new UpdateCheckMark { Width = 14, Height = 14, IsVisible = false };
    private readonly Grid _playSections = new() { ColumnDefinitions = new("*,56") };
    private readonly Border _playSplit = new()
    {
        Name = "PlaySplitButton", HorizontalAlignment = HorizontalAlignment.Center,
        Background = PartyRoom.Coral, CornerRadius = new CornerRadius(20), ClipToBounds = true
    };
    private readonly ToggleButton _settingsNavigation = new() { Name = "SettingsNavigation" };
    private Control? _homeWordmark;
    private double _wordmarkSize;
    private int _heroLayout = -1;

    private static Control Wordmark(double fontSize)
    {
        var title = PartyRoom.Heading("LUCKER\nPARTY", fontSize);
        title.LineHeight = fontSize * .9; title.TextAlignment = TextAlignment.Center;
        var mark = new Grid { ColumnDefinitions = new("Auto,Auto") };
        Place(mark, title);
        Place(mark, new BrandAccents
        {
            Width = fontSize * .82, Height = fontSize,
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(fontSize * .08, 0, 0, 0)
        }, column: 1);
        AutomationProperties.SetName(mark, "Lucker Party");
        return mark;
    }

    // This header stays visible on both pages. Version/status belong to the selected channel.
    private Control BuildHeader()
    {
        var header = new Grid { RowDefinitions = new("40,Auto") };
        Place(header, BuildCaptionRow());
        var channel = new StackPanel
        {
            Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(24, 3, 24, 5)
        };
        channel.Children.Add(ChannelSelector());
        var version = PartyRoom.Text(_controller.Version, 12, PartyRoom.Muted);
        version.TextAlignment = TextAlignment.Right; version.Margin = new Thickness(0, 2, 8, 0);
        channel.Children.Add(version);
        var status = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 5,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 8, 0)
        };
        _updateStatus.FontSize = 12; _updateStatus.TextAlignment = TextAlignment.Right;
        _updateCheck.VerticalAlignment = VerticalAlignment.Center;
        status.Children.Add(_updateCheck); status.Children.Add(_updateStatus);
        channel.Children.Add(status); Place(header, channel, 1);
        return header;
    }

    private Control BuildHome()
    {
        var home = new Grid { RowDefinitions = new("*,Auto,Auto,Auto"), Margin = new Thickness(28, 0, 28, 12) };
        _tagline.LetterSpacing = 1.1; _tagline.FontWeight = FontWeight.Bold;
        _heroTitle.Children.Add(_tagline);
        using var stream = AssetLoader.Open(new Uri("avares://LuckerParty.Launcher/Assets/party-room-arena.png"));
        _arena.Source = new Bitmap(stream); _arena.VerticalAlignment = VerticalAlignment.Center;
        _arena.HorizontalAlignment = HorizontalAlignment.Right;
        var backdrop = new PartyRoomBackdrop { IsHitTestVisible = false };
        Grid.SetRowSpan(backdrop, 2); Grid.SetColumnSpan(backdrop, 2); Place(_heroArea, backdrop);
        Place(_heroArea, _arena, 1); Place(_heroArea, _heroTitle);
        Place(home, _heroArea);
        var headline = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 14) };
        _headline.TextAlignment = TextAlignment.Center;
        var subtitle = PartyRoom.Text("New chaos. Same crew.", 17, PartyRoom.Muted);
        subtitle.TextAlignment = TextAlignment.Center; subtitle.FontWeight = FontWeight.Bold;
        headline.Children.Add(_headline); headline.Children.Add(subtitle); Place(home, headline, 1);
        var playContent = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        playContent.Children.Add(new LauncherIcon("play") { Width = 24, Height = 24, Foreground = Brushes.White });
        playContent.Children.Add(_playLabel); _play.Content = playContent;
        _play.HorizontalAlignment = HorizontalAlignment.Stretch; _play.VerticalAlignment = VerticalAlignment.Stretch;
        _more.HorizontalAlignment = HorizontalAlignment.Stretch; _more.VerticalAlignment = VerticalAlignment.Stretch;
        _play.CornerRadius = _more.CornerRadius = new CornerRadius(0);
        _more.Padding = new Thickness(16);
        _more.BorderThickness = new Thickness(1, 0, 0, 0); _more.BorderBrush = PartyRoom.Brush("#FFB0B3");
        Place(_playSections, _play); Place(_playSections, _more, column: 1);
        _playSplit.Child = _playSections; Place(home, _playSplit, 2);
        var details = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0), MaxWidth = 640 };
        _message.TextAlignment = TextAlignment.Center;
        details.Children.Add(_message); details.Children.Add(_progress); Place(home, details, 3);
        UpdateHomeLayout(); return home;
    }

    private void UpdateHomeLayout()
    {
        var windowWidth = Bounds.Width > 0 ? Bounds.Width : Width;
        var windowHeight = Bounds.Height > 0 ? Bounds.Height : Height;
        var width = windowWidth - (_controller.AllowMultipleInstances ? 304 : 0) - 56;
        var shortWindow = windowHeight < 780;
        var narrow = width < 680;
        var compact = narrow || shortWindow;
        var sideBySide = shortWindow && !narrow;
        var overlay = !compact;
        var layout = overlay ? 2 : sideBySide ? 1 : 0;
        if (_heroLayout != layout)
        {
            _heroArea.RowDefinitions = new(overlay || sideBySide ? "*" : "Auto,*");
            _heroArea.ColumnDefinitions = new(sideBySide ? "Auto,*" : "*");
            Grid.SetRow(_arena, overlay || sideBySide ? 0 : 1);
            Grid.SetColumn(_arena, sideBySide ? 1 : 0);
            _heroLayout = layout;
        }
        var size = compact ? 48d : 72d;
        if (_homeWordmark is null || _wordmarkSize != size)
        {
            if (_homeWordmark is not null) _heroTitle.Children.Remove(_homeWordmark);
            _homeWordmark = Wordmark(size); _wordmarkSize = size; _heroTitle.Children.Add(_homeWordmark);
        }
        _heroTitle.HorizontalAlignment = HorizontalAlignment.Left;
        _heroTitle.VerticalAlignment = VerticalAlignment.Top;
        _heroTitle.Margin = new Thickness(6, 0, sideBySide ? 14 : 0, 0);
        _tagline.FontSize = compact ? 10 : 12;
        _tagline.HorizontalAlignment = HorizontalAlignment.Left;
        _headline.FontSize = compact ? 30 : 42;
        // The image has an empty upper-left corner. The wide layout shares it
        // with the brand, leaving more room for the arena and the main action.
        // A short window uses columns; a narrow sidebar layout uses rows.
        _arena.HorizontalAlignment = sideBySide ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        _arena.VerticalAlignment = VerticalAlignment.Bottom;
        _arena.MaxWidth = overlay ? width * .9 : double.PositiveInfinity;
        _arena.Margin = new Thickness(0, overlay ? 40 : 0, 0, 0);
        var playHeight = compact ? 58d : 64d;
        _playSplit.Width = Math.Min(compact ? 350 : 410, Math.Max(0, width));
        _playSplit.Height = _playSections.Height = _play.Height = _more.Height = playHeight;
        _more.Width = playHeight;
        _more.Padding = new Thickness((playHeight - 24) / 2);
        _playSections.ColumnDefinitions[1].Width = new GridLength(playHeight);
        _playLabel.FontSize = compact ? 22 : 24;
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
        var footer = new Grid { Margin = new Thickness(24, 0, 24, 12) };
        var settingsContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        settingsContent.Children.Add(new LauncherIcon("gear") { Width = 24, Height = 24 });
        var text = PartyRoom.Text("Settings", 17); text.FontWeight = FontWeight.Bold; settingsContent.Children.Add(text);
        _settingsNavigation.Content = settingsContent;
        _settingsNavigation.HorizontalAlignment = HorizontalAlignment.Left;
        _settingsNavigation.HorizontalContentAlignment = HorizontalAlignment.Center;
        _settingsNavigation.VerticalContentAlignment = VerticalAlignment.Center;
        _settingsNavigation.Padding = new Thickness(18, 12);
        _settingsNavigation.Height = 50; _settingsNavigation.MinWidth = 154;
        _settingsNavigation.CornerRadius = new CornerRadius(16);
        _settingsNavigation.Classes.Add("settings-nav");
        AutomationProperties.SetName(_settingsNavigation, "Settings");
        _settingsNavigation.IsCheckedChanged += (_, _) => SetSettingsVisible(_settingsNavigation.IsChecked == true);
        Place(footer, _settingsNavigation);
        return new Border
        {
            BorderBrush = PartyRoom.Line, BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 10, 0, 0), Child = footer
        };
    }
}

internal sealed class BrandAccents : Control
{
    public override void Render(DrawingContext context)
    {
        var unit = Bounds.Width / 54;
        DrawSquare(context, new Rect(12 * unit, 6 * unit, 19 * unit, 19 * unit), "#FF575E", -7);
        DrawSquare(context, new Rect(2 * unit, 34 * unit, 19 * unit, 19 * unit), "#FF9D22", -5);
        DrawSquare(context, new Rect(32 * unit, 28 * unit, 19 * unit, 19 * unit), "#AD7DFF", 14);
    }

    private static void DrawSquare(DrawingContext context, Rect bounds, string color, double degrees)
    {
        var center = bounds.Center;
        var transform = Matrix.CreateTranslation(-center.X, -center.Y)
            * Matrix.CreateRotation(degrees * Math.PI / 180) * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(transform)) context.DrawRectangle(PartyRoom.Brush(color), null, bounds, 3, 3);
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

// A few soft marks give the ivory canvas depth without adding a competing image.
internal sealed class PartyRoomBackdrop : Control
{
    public override void Render(DrawingContext context)
    {
        var lilac = PartyRoom.Brush("#22AD7DFF");
        var coral = PartyRoom.Brush("#18FF575E");
        context.DrawEllipse(lilac, null, new Point(Bounds.Width * .055, Bounds.Height * .72), 5, 5);
        context.DrawEllipse(coral, null, new Point(Bounds.Width * .085, Bounds.Height * .76), 3, 3);
        context.DrawEllipse(lilac, null, new Point(Bounds.Width * .93, Bounds.Height * .07), 4, 4);
        var pen = new Pen(PartyRoom.Brush("#20FF9D22"), 2);
        var x = Bounds.Width * .88; var y = Bounds.Height * .035;
        context.DrawLine(pen, new Point(x - 4, y), new Point(x + 4, y));
        context.DrawLine(pen, new Point(x, y - 4), new Point(x, y + 4));
    }
}
