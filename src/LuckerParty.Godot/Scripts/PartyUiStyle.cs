using Godot;

namespace LuckerParty.Godot;

/// <summary>Small shared set of native Godot styles for the party screens.</summary>
internal static class PartyUiStyle
{
    public static Color Ink => new("111b34");
    public static Color Muted => new("667087");
    public static Color Cream => new("fffaf2");
    public static Color Sand => new("f2ecdf");
    public static Color Coral => new("ff5367");
    public static Color Green => new("148765");

    public static StyleBoxFlat Box(Color fill, int radius = 18, int border = 0, Color? edge = null,
        int padding = 18)
    {
        return new StyleBoxFlat
        {
            BgColor = fill, BorderColor = edge ?? new Color("e5dcd0"),
            BorderWidthLeft = border, BorderWidthRight = border, BorderWidthTop = border, BorderWidthBottom = border,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = padding, ContentMarginRight = padding,
            ContentMarginTop = padding, ContentMarginBottom = padding
        };
    }

    public static void Button(Button button, bool secondary = false)
    {
        var fill = secondary ? new Color("eee9e1") : Coral;
        var hover = secondary ? new Color("e5ded4") : new Color("ed4058");
        var pressed = secondary ? new Color("d9d1c5") : new Color("d9364e");
        button.AddThemeStyleboxOverride("normal", Box(fill, 15, padding: 14));
        button.AddThemeStyleboxOverride("hover", Box(hover, 15, padding: 14));
        button.AddThemeStyleboxOverride("pressed", Box(pressed, 15, padding: 14));
        button.AddThemeStyleboxOverride("disabled", Box(new Color("e6e3df"), 15, padding: 14));
        var color = secondary ? Ink : Colors.White;
        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color" })
            button.AddThemeColorOverride(name, color);
        button.AddThemeColorOverride("font_disabled_color", Muted);
    }

    public static Label Label(string text, int size = 18, bool muted = false, bool heading = false)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.Off };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", muted ? Muted : Ink);
        if (heading) label.ThemeTypeVariation = "Heading";
        return label;
    }
}
