namespace LuckerParty;

/// <summary>
///     A Client who is watching a Round but not participating.
/// </summary>
public class Spectator : Component
{
	public Client Client => Components.Get<Client>();
}
