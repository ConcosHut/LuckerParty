using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace LuckerParty.Launcher;

// Render the approved, font-free SVG paths directly. This intentionally supports
// only the paths, inherited fills and translate/scale used by our three assets.
internal sealed class BrandLogo : Control
{
    private sealed record Shape(Geometry Geometry, IBrush Fill, Matrix Transform);
    private sealed record Artwork(Rect ViewBox, Shape[] Shapes);
    private static readonly Dictionary<string, Artwork> Cache = new();
    private readonly Artwork _art;

    public BrandLogo(string asset)
    {
        if (!Cache.TryGetValue(asset, out var artwork)) Cache[asset] = artwork = Load(asset);
        _art = artwork;
        IsHitTestVisible = false;
        AutomationProperties.SetName(this, "Lucker Party");
    }

    public override void Render(DrawingContext context)
    {
        var scale = Math.Min(Bounds.Width / _art.ViewBox.Width, Bounds.Height / _art.ViewBox.Height);
        var x = (Bounds.Width - _art.ViewBox.Width * scale) / 2;
        var y = (Bounds.Height - _art.ViewBox.Height * scale) / 2;
        var fit = Matrix.CreateTranslation(-_art.ViewBox.X, -_art.ViewBox.Y)
            * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y);
        foreach (var shape in _art.Shapes)
        {
            using var transform = context.PushTransform(shape.Transform * fit);
            context.DrawGeometry(shape.Fill, null, shape.Geometry);
        }
    }

    private static Artwork Load(string asset)
    {
        using var stream = typeof(BrandLogo).Assembly.GetManifestResourceStream($"LuckerParty.Brand.lucker-party-{asset}.svg")
            ?? throw new InvalidOperationException($"Missing brand asset: {asset}");
        var root = XDocument.Load(stream).Root!;
        var box = Numbers(root.Attribute("viewBox")!.Value);
        var shapes = root.Descendants().Where(node => node.Name.LocalName == "path").Select(path =>
        {
            var geometry = PathGeometry.Parse(path.Attribute("d")!.Value);
            geometry.FillRule = FillRule.EvenOdd;
            var parents = path.AncestorsAndSelf().ToArray();
            var fill = parents.Select(node => node.Attribute("fill")?.Value).First(value => value is not null)!;
            var matrix = Matrix.Identity;
            foreach (var parent in parents)
            {
                if (parent.Attribute("transform") is not { } attribute) continue;
                var local = Matrix.Identity;
                foreach (Match match in Regex.Matches(attribute.Value, @"(translate|scale)\(([^)]+)\)"))
                {
                    var numbers = Numbers(match.Groups[2].Value);
                    var operation = match.Groups[1].Value == "translate"
                        ? Matrix.CreateTranslation(numbers[0], numbers.Length > 1 ? numbers[1] : 0)
                        : Matrix.CreateScale(numbers[0], numbers.Length > 1 ? numbers[1] : numbers[0]);
                    // SVG lists transforms outside-in; Avalonia uses row vectors.
                    local = operation * local;
                }
                matrix *= local;
            }
            return new Shape(geometry, PartyRoom.Brush(fill), matrix);
        }).ToArray();
        return new(new Rect(box[0], box[1], box[2], box[3]), shapes);
    }

    private static double[] Numbers(string text) => text.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
}
