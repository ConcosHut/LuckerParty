using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace LuckerParty.Launcher;

internal static class PartyRoom
{
    public static readonly IBrush Ivory = Brush("#FFFDF8"), Sand = Brush("#F1E7D7"), Ink = Brush("#20243B"),
        Muted = Brush("#636884"), Coral = Brush("#FF6B76"), Lilac = Brush("#E7DEFF"),
        Line = Brush("#E8E1D8"), Green = Brush("#278568"), Card = Brush("#FCF8F1");
    public static readonly FontFamily Body = new("avares://LuckerParty.Launcher/Assets/Fonts#Nunito Sans");
    public static readonly FontFamily Display = new("avares://LuckerParty.Launcher/Assets/Fonts#Lilita One");
    public static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
    public static TextBlock Text(string text, double size = 14, IBrush? color = null) => new()
    { Text = text, FontSize = size, Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap };
    public static TextBlock Heading(string text, double size) => new()
    { Text = text, FontFamily = Display, FontSize = size, Foreground = Ink, TextWrapping = TextWrapping.Wrap };

    public static Styles ButtonStyles()
        => (Styles)AvaloniaXamlLoader.Load(new Uri("avares://LuckerParty.Launcher/Assets/PartyRoomControls.axaml"));

    public static void StyleMenu(ContextMenu menu)
    {
        // Popup surfaces are separate visual roots: give the menu and its item
        // their own themes instead of relying on a window descendant selector.
        var styles = ButtonStyles();
        menu.Theme = (ControlTheme)styles.Resources["PartyRoomMenuTheme"]!;
        menu.ItemContainerTheme = (ControlTheme)styles.Resources["PartyRoomMenuItemTheme"]!;
        menu.FontFamily = Body;
    }
}

internal sealed class LauncherIcon(string kind) : Control
{
    public string Kind
    {
        get => kind;
        set { if (kind == value) return; kind = value; InvalidateVisual(); }
    }
    public override void Render(DrawingContext context)
    {
        var pen = new Pen(Foreground ?? PartyRoom.Ink, 2);
        if (kind == "play") context.DrawGeometry(Foreground ?? PartyRoom.Ink, null, Geometry.Parse("M 5,2 L 19,12 L 5,22 Z"));
        else if (kind == "chevron") context.DrawGeometry(null, pen, Geometry.Parse("M 5,9 L 12,16 L 19,9"));
        else if (kind == "download") context.DrawGeometry(null, pen, Geometry.Parse("M 12,2 L 12,15 M 6,10 L 12,16 L 18,10 M 4,17 L 4,22 L 20,22 L 20,17"));
        else
        {
            context.DrawEllipse(null, pen, new Point(12, 12), 7, 7);
            context.DrawEllipse(null, pen, new Point(12, 12), 2.5, 2.5);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Math.PI / 4;
                context.DrawLine(pen, new Point(12 + Math.Cos(angle) * 8, 12 + Math.Sin(angle) * 8),
                    new Point(12 + Math.Cos(angle) * 11, 12 + Math.Sin(angle) * 11));
            }
        }
    }
    public IBrush? Foreground { get; init; }
}
