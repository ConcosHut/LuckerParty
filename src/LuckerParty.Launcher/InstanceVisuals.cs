using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LuckerParty.Launcher;

/// <summary>Small, resolution-independent artwork using the arena's capsule palette and lighting.</summary>
internal sealed class CapsuleThumbnail : Control
{
    private static readonly Geometry Floor = Geometry.Parse("M 5,72 L 33,61 L 63,74 L 35,86 Z");
    private static readonly Geometry FloorEdge = Geometry.Parse("M 5,72 L 35,86 L 63,74 L 63,77 L 35,89 L 5,75 Z");
    private static readonly string[][] Palette =
    [
        ["#FFB4D7", "#FF58A0", "#EF2E7B", "#B9185B", "#FBE8EF", "#EFD4E1"],
        ["#FFD9A3", "#FFA92C", "#FA8409", "#BA4E0A", "#FFF0DD", "#EDDCC8"],
        ["#DAB1FF", "#A163FF", "#8136E9", "#491FA5", "#F0E8FF", "#E1D6EE"]
    ];
    private readonly IBrush _background, _floor, _edge, _body;
    private readonly IBrush _highlight = new RadialGradientBrush
    {
        Center = new RelativePoint(.35, .25, RelativeUnit.Relative),
        GradientOrigin = new RelativePoint(.35, .25, RelativeUnit.Relative),
        GradientStops = new GradientStops { new(Color.Parse("#BFFFFFFF"), 0), new(Color.Parse("#00FFFFFF"), 1) }
    };

    public CapsuleThumbnail(int number)
    {
        var colors = Palette[Math.Abs(number - 1) % Palette.Length];
        _background = PartyRoom.Brush(colors[4]); _floor = PartyRoom.Brush(colors[5]); _edge = PartyRoom.Brush("#D5C9BE");
        _body = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, .25, RelativeUnit.Relative), EndPoint = new RelativePoint(1, .65, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new(Color.Parse(colors[0]), 0), new(Color.Parse(colors[1]), .27),
                new(Color.Parse(colors[2]), .68), new(Color.Parse(colors[3]), 1)
            }
        };
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        using var scale = context.PushTransform(Matrix.CreateScale(Bounds.Width / 68, Bounds.Height / 90));
        context.DrawRectangle(_background, null, new Rect(0, 0, 68, 90), 11, 11);
        context.DrawGeometry(_edge, null, FloorEdge); context.DrawGeometry(_floor, null, Floor);
        context.DrawEllipse(PartyRoom.Brush("#0F382623"), null, new Point(36, 74), 19, 5);
        context.DrawEllipse(PartyRoom.Brush("#19382623"), null, new Point(36, 74), 15, 3.5);
        var body = new Rect(18, 13, 34, 62);
        context.DrawRectangle(_body, new Pen(PartyRoom.Brush("#16FFFFFF"), .7), body, 17, 17);
        context.DrawRectangle(_highlight, null, body, 17, 17);
        context.DrawRectangle(PartyRoom.Ink, null, new Rect(29, 37, 3.5, 11), 2, 2);
        context.DrawRectangle(PartyRoom.Ink, null, new Rect(39, 37, 3.5, 11), 2, 2);
    }
}

internal sealed class InstanceActionIcon(string kind) : Control
{
    private static readonly Geometry Eye = Geometry.Parse("M 1,6.5 C 4,1 9,1 12,6.5 C 9,12 4,12 1,6.5 Z");
    private static readonly Geometry Close = Geometry.Parse("M 2,2 L 11,11 M 11,2 L 2,11");

    public override void Render(DrawingContext context)
    {
        using var scale = context.PushTransform(Matrix.CreateScale(Bounds.Width / 13, Bounds.Height / 13));
        var pen = new Pen(PartyRoom.Ink, 1.5);
        if (kind == "eye")
        {
            context.DrawGeometry(null, pen, Eye);
            context.DrawEllipse(PartyRoom.Ink, null, new Point(6.5, 6.5), 1.7, 1.7);
        }
        else context.DrawGeometry(null, pen, Close);
    }
}
