namespace LuckerParty;

/// <summary>
///     Listens to Network events and creates Client GameObjects
/// </summary>
public sealed class NetworkManager : Component, Component.INetworkListener
{
	[Property] private GameObject ClientGroup { get; set; }

	public void OnActive( Connection channel )
	{
		var gameObject = new GameObject( ClientGroup ) { Name = $"{channel.DisplayName} ({channel.SteamId})" };
		gameObject.NetworkSpawn( channel );

		var client = gameObject.AddComponent<Client>();
		client.Connection = channel;
	}
}
