namespace LuckerParty;

/// <summary>
///     A Client who is playing in a round. Created in the Lobby and persisted throughout the Round.
/// </summary>
public class Player : Component
{
	public PlayerColor PlayerColor = PlayerColor.Cyan;
	public Client Client => Components.Get<Client>();
}
