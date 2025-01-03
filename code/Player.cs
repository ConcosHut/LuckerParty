namespace LuckerParty;

/// <summary>
///     A Client who is playing in a round. Created in the Lobby and persisted throughout the Round.
/// </summary>
public class Player : Component
{
	public Client Client => Components.Get<Client>();
	public PlayerColor PlayerColor { get; set; }
}
