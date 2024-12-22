namespace LuckerParty;

/// <summary>
///     Listens to Network events and creates Client GameObjects
/// </summary>
public sealed class NetworkManager : Component, Component.INetworkListener
{
	/// <summary>
	///     A GameObject used for organizational grouping of Clients
	/// </summary>
	[Property]
	private GameObject ClientGroup { get; set; }

	public void OnActive( Connection channel )
	{
		ClientGroup ??= new GameObject( Scene.Root );

		var gameObject = new GameObject( ClientGroup ) { Name = $"{channel.DisplayName} ({channel.SteamId})" };
		gameObject.NetworkSpawn( channel );

		var client = gameObject.AddComponent<Client>();
		client.Connection = channel;
	}
}
