using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
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
    private Control? _homeWordmark;
    private double _wordmarkSize;
    private int _heroLayout;

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
        Place(_heroArea, _arena, 1); Place(_heroArea, _heroTitle);
        Place(home, _heroArea);
        var headline = new StackPanel { Spacing = 2, Margin = new Thickness(0, 4, 0, 16) };
        _headline.TextAlignment = TextAlignment.Center;
        var subtitle = PartyRoom.Text("New chaos. Same crew.", 17, PartyRoom.Muted);
        subtitle.TextAlignment = TextAlignment.Center; subtitle.FontWeight = FontWeight.Bold;
        headline.Children.Add(_headline); headline.Children.Add(subtitle); Place(home, headline, 1);
        var sections = new Grid { ColumnDefinitions = new("*,52"), Height = 52 };
        var playContent = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        playContent.Children.Add(new LauncherIcon("play") { Width = 24, Height = 24, Foreground = Brushes.White });
        playContent.Children.Add(_playLabel); _play.Content = playContent;
        _play.HorizontalAlignment = HorizontalAlignment.Stretch; _play.VerticalAlignment = VerticalAlignment.Stretch;
        _more.HorizontalAlignment = HorizontalAlignment.Stretch; _more.VerticalAlignment = VerticalAlignment.Stretch;
        _play.Height = _more.Height = 52; _more.Width = 52;
        _play.CornerRadius = _more.CornerRadius = new CornerRadius(0); _more.Padding = new Thickness(14);
        _more.BorderThickness = new Thickness(1, 0, 0, 0); _more.BorderBrush = PartyRoom.Brush("#FFB0B3");
        Place(sections, _play); Place(sections, _more, column: 1);
        Place(home, new Border
        {
            Name = "PlaySplitButton", Width = 310, Height = 52, HorizontalAlignment = HorizontalAlignment.Center,
            Background = PartyRoom.Coral, CornerRadius = new CornerRadius(18), ClipToBounds = true, Child = sections
        }, 2);
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
        var shortWindow = windowHeight < 720;
        var compact = width < 680 || shortWindow;
        var asymmetric = _controller.AllowMultipleInstances && !compact;
        var sideBySide = shortWindow && !_controller.AllowMultipleInstances;
        var layout = asymmetric ? 2 : sideBySide ? 1 : 0;
        if (_heroLayout != layout)
        {
            _heroArea.RowDefinitions = new(sideBySide || asymmetric ? "*" : "Auto,*");
            _heroArea.ColumnDefinitions = new(sideBySide ? "Auto,*" : "*");
            Grid.SetRow(_arena, sideBySide || asymmetric ? 0 : 1);
            Grid.SetColumn(_arena, sideBySide ? 1 : 0);
            _heroLayout = layout;
        }
        var size = compact ? 48d : 74d;
        if (_homeWordmark is null || _wordmarkSize != size)
        {
            if (_homeWordmark is not null) _heroTitle.Children.Remove(_homeWordmark);
            _homeWordmark = Wordmark(size); _wordmarkSize = size; _heroTitle.Children.Add(_homeWordmark);
        }
        _heroTitle.HorizontalAlignment = asymmetric || sideBySide ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        _arena.HorizontalAlignment = asymmetric || sideBySide ? HorizontalAlignment.Right : HorizontalAlignment.Center;
        _arena.VerticalAlignment = asymmetric ? VerticalAlignment.Bottom : VerticalAlignment.Center;
        _heroTitle.VerticalAlignment = sideBySide ? VerticalAlignment.Center : VerticalAlignment.Top;
        _heroTitle.Margin = new Thickness(asymmetric ? 12 : 0, 0, sideBySide ? 16 : 0, 0);
        _tagline.FontSize = compact ? 10 : 12;
        _tagline.HorizontalAlignment = asymmetric ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        _headline.FontSize = compact ? 28 : 40;
        // Only the wide sidebar composition uses the arena's empty upper-left
        // corner. The inset keeps its rear walls clear of the fixed-size wordmark.
        // Centered and compact modes retain separate rows or columns.
        _arena.MaxHeight = double.PositiveInfinity;
        _arena.Margin = asymmetric ? new Thickness(96, 18, 0, 0)
            : new Thickness(0, sideBySide ? 0 : 8, 0, 0);
    }

    private Control BuildSettings()
    {
        var content = new StackPanel { Spacing = 20, Margin = new Thickness(28, 0, 28, 24) };
        var back = Button("← Back to play"); back.HorizontalAlignment = HorizontalAlignment.Left;
        back.Click += (_, _) => { _home.IsVisible = true; _settings.IsVisible = false; UpdateHomeLayout(); };
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
        var settingsContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        settingsContent.Children.Add(new LauncherIcon("gear") { Width = 24, Height = 24 });
        var text = PartyRoom.Text("Settings", 15); text.FontWeight = FontWeight.Bold; settingsContent.Children.Add(text);
        var settings = Button(settingsContent); settings.HorizontalAlignment = HorizontalAlignment.Left;
        AutomationProperties.SetName(settings, "Settings");
        settings.Click += (_, _) => { _home.IsVisible = false; _settings.IsVisible = true; };
        Place(footer, settings);
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
