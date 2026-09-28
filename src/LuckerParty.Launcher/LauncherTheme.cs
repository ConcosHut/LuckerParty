using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace LuckerParty.Launcher;

internal static class PartyRoom
{
    public static readonly IBrush Ivory = Brush("#FFFDF8"), Sand = Brush("#F0E5D4"), Ink = Brush("#10192D"),
        Muted = Brush("#636884"), Coral = Brush("#FF575E"), Lilac = Brush("#E7DEFF"),
        Line = Brush("#E8E1D8"), Green = Brush("#19B981"), Card = Brush("#FCF8F1");
    public static readonly FontFamily Body = new("avares://LuckerParty.Launcher/Assets/Fonts#Nunito Sans");
    public static readonly FontFamily Display = new("avares://LuckerParty.Launcher/Assets/Fonts#Lilita One");
    public static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
    public static TextBlock Text(string text, double size = 14, IBrush? color = null) => new()
    { Text = text, FontSize = size, Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap };
    public static TextBlock Heading(string text, double size) => new()
    { Text = text, FontFamily = Display, FontSize = size, Foreground = Ink, TextWrapping = TextWrapping.Wrap };

    public static Styles ButtonStyles()
    {
        var styles = new Styles();
        var normal = new Style(selector => selector.OfType<Button>());
        normal.Setters.Add(new Setter(Button.CornerRadiusProperty, new CornerRadius(12)));
        normal.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
        normal.Setters.Add(new Setter(Button.FontWeightProperty, FontWeight.Bold));
        styles.Add(normal);
        var hover = new Style(selector => selector.OfType<Button>().Class("flat").Class(":pointerover"));
        hover.Setters.Add(new Setter(Button.BackgroundProperty, Lilac));
        hover.Setters.Add(new Setter(Button.ForegroundProperty, Ink));
        styles.Add(hover);
        var actionHover = new Style(selector => selector.OfType<Button>().Class("primary").Class(":pointerover"));
        actionHover.Setters.Add(new Setter(Button.BackgroundProperty, Brush("#EB474F")));
        actionHover.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
        styles.Add(actionHover);
        return styles;
    }
}

internal sealed class CapsuleAvatar(int number) : Control
{
    public override void Render(DrawingContext context)
    {
        var color = new[] { "#F94D9B", "#FF9D22", "#8E45ED" }[(number - 1) % 3];
        context.DrawEllipse(PartyRoom.Brush("#DED2C5"), null, new Point(32, 72), 20, 5);
        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops { new(Colors.White, 0), new(Color.Parse(color), .35), new(Color.Parse(color), 1) }
        };
        context.DrawRectangle(gradient, null, new Rect(13, 9, 38, 62), 19, 19);
        context.DrawRectangle(PartyRoom.Ink, null, new Rect(26, 31, 3, 11), 2, 2);
        context.DrawRectangle(PartyRoom.Ink, null, new Rect(35, 31, 3, 11), 2, 2);
    }
}

internal sealed class LauncherIcon(string kind) : Control
{
    public override void Render(DrawingContext context)
    {
        var pen = new Pen(Foreground ?? PartyRoom.Ink, 2);
        if (kind == "play") context.DrawGeometry(Foreground ?? PartyRoom.Ink, null, Geometry.Parse("M 5,2 L 19,12 L 5,22 Z"));
        else if (kind == "chevron") context.DrawGeometry(null, pen, Geometry.Parse("M 5,9 L 12,16 L 19,9"));
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
