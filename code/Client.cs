using LuckerParty.UI;

namespace LuckerParty;

/// <summary>
///     A Lucker Party Client. Entry point for a user (or bot?) to get a UI and become a Spectator or Lucker after they've
///     connected to the lobby.
/// </summary>
public sealed class Client : Component
{
	public Connection Connection { get; set; }

	protected override void OnStart()
	{
		GameObject.AddComponent<ScreenPanel>();
		GameObject.AddComponent<Hud>();
	}
}
