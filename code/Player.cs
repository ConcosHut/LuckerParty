namespace LuckerParty;

public class Player : Component
{
	public Client Client => Components.Get<Client>();
	public PlayerColor PlayerColor { get; set; }
}
