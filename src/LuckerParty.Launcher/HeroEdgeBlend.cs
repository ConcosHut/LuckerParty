using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LuckerParty.Launcher;

/// <summary>Softens the approved hero's opaque matte into the surrounding surface.</summary>
internal sealed class HeroEdgeBlend(Image image, Panel backdrop) : Control
{
    public override void Render(DrawingContext context)
    {
        if (image.Source is null || backdrop.Background is null ||
            image.TranslatePoint(default, this) is not { } imageOrigin ||
            backdrop.TranslatePoint(default, this) is not { } backdropOrigin) return;

        var scale = image.Stretch.CalculateScaling(image.Bounds.Size, image.Source.Size, image.StretchDirection);
        var artwork = new Rect(imageOrigin, image.Bounds.Size).CenterRect(new Rect(image.Source.Size * scale));
        if (artwork.Width <= 0 || artwork.Height <= 0) return;
        var surface = new Rect(backdropOrigin, backdrop.Bounds.Size);
        // Keep the band inside the blank margin, away from the platform geometry.
        var feather = artwork.Width * .0125;
        Fade(new Rect(artwork.X, artwork.Y, artwork.Width, feather), new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(0, 1, RelativeUnit.Relative));
        Fade(new Rect(artwork.X, artwork.Bottom - feather, artwork.Width, feather), new RelativePoint(0, 1, RelativeUnit.Relative), new RelativePoint(0, 0, RelativeUnit.Relative));
        Fade(new Rect(artwork.X, artwork.Y, feather, artwork.Height), new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(1, 0, RelativeUnit.Relative));
        Fade(new Rect(artwork.Right - feather, artwork.Y, feather, artwork.Height), new RelativePoint(1, 0, RelativeUnit.Relative), new RelativePoint(0, 0, RelativeUnit.Relative));

        void Fade(Rect edge, RelativePoint outside, RelativePoint inside)
        {
            var mask = new LinearGradientBrush
            {
                StartPoint = outside, EndPoint = inside,
                GradientStops = new GradientStops { new GradientStop(Colors.White, 0), new GradientStop(Colors.Transparent, 1) }
            };
            using (context.PushClip(edge))
            using (context.PushOpacityMask(mask, edge))
                // Draw the actual surface at its original coordinates, so the
                // gradient stays aligned while resizing or revealing the sidebar.
                context.DrawRectangle(backdrop.Background, null, surface);
        }
    }
}
