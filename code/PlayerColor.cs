using System.Collections.Immutable;

namespace LuckerParty;

/// <summary>
///     Represents the Color of a Player. There are 22 supported colors.
///     These were sourced from https://sashamaps.net/docs/resources/20-colors/
/// </summary>
/// <param name="Name">Human-readable name of the color</param>
/// <param name="Color">Sandbox Color value</param>
public readonly record struct PlayerColor( string Name, Color Color )
{
	public static readonly PlayerColor Red = new("Red", new Color( 230, 25, 75 ));
	public static readonly PlayerColor Green = new("Green", new Color( 60, 180, 75 ));
	public static readonly PlayerColor Yellow = new("Yellow", new Color( 255, 255, 25 ));
	public static readonly PlayerColor Blue = new("Blue", new Color( 0, 130, 200 ));
	public static readonly PlayerColor Orange = new("Orange", new Color( 245, 130, 48 ));
	public static readonly PlayerColor Purple = new("Purple", new Color( 145, 30, 180 ));
	public static readonly PlayerColor Cyan = new("Cyan", new Color( 70, 240, 240 ));
	public static readonly PlayerColor Magenta = new("Magenta", new Color( 240, 50, 230 ));
	public static readonly PlayerColor Lime = new("Lime", new Color( 210, 245, 230 ));
	public static readonly PlayerColor Pink = new("Pink", new Color( 250, 190, 212 ));
	public static readonly PlayerColor Teal = new("Teal", new Color( 0, 128, 128 ));
	public static readonly PlayerColor Lavender = new("Lavender", new Color( 220, 190, 255 ));
	public static readonly PlayerColor Brown = new("Brown", new Color( 170, 110, 40 ));
	public static readonly PlayerColor Beige = new("Beige", new Color( 255, 250, 200 ));
	public static readonly PlayerColor Maroon = new("Maroon", new Color( 128, 0, 0 ));
	public static readonly PlayerColor Mint = new("Mint", new Color( 170, 255, 195 ));
	public static readonly PlayerColor Olive = new("Olive", new Color( 128, 128, 0 ));
	public static readonly PlayerColor Apricot = new("Apricot", new Color( 255, 215, 180 ));
	public static readonly PlayerColor Navy = new("Navy", new Color( 0, 0, 128 ));
	public static readonly PlayerColor Gray = new("Gray", new Color( 128, 128, 128 ));
	public static readonly PlayerColor White = new("White", new Color( 255, 255, 255 ));
	public static readonly PlayerColor Black = new("Black", new Color( 0, 0, 0 ));

	/// <summary>
	///     Ordered list of supported colors
	/// </summary>
	// ReSharper disable once RedundantExplicitParamsArrayCreation
	public static readonly ImmutableList<PlayerColor> All = ImmutableList.Create( new[]
	{
		Red, Green, Yellow, Blue, Orange, Purple, Cyan, Magenta, Lime, Pink, Teal, Lavender, Brown, Beige, Maroon,
		Mint, Olive, Apricot, Navy, Gray, White, Black
	} );

	/// <summary>
	///     Set of currently used colors. Used to ensure no two players have the same color
	/// </summary>
	public static HashSet<PlayerColor> Used = new();
}
