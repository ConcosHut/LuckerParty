namespace LuckerParty;

/// <summary>
///     Listens to Network events and creates Client GameObjects
/// </summary>
public sealed class NetworkManager : Component, Component.INetworkListener
{
	/// <summary>
	///     Map from Connection to Client GameObject
	/// </summary>
	private readonly Dictionary<Connection, GameObject> _clientMap = new();

	/// <summary>
	///     A GameObject used for organizational grouping of Clients
	/// </summary>
	private GameObject ClientGroup { get; set; }

	public void OnActive( Connection channel )
	{
		// Set up the Client GameObject
		var gameObject = new GameObject( ClientGroup ) { Name = $"{channel.DisplayName} ({channel.SteamId})" };
		_clientMap.Add( channel, gameObject );
		var client = gameObject.AddComponent<Client>();
		client.Connection = channel;

		// Spawn it on remote clients
		gameObject.NetworkSpawn( channel );
	}

	public void OnDisconnected( Connection channel )
	{
		if ( !_clientMap.TryGetValue( channel, out var clientGameObject ) )
		{
			Log.Warning( $"Disconnected client {channel.SteamId} has no associated GameObject." );
			return;
		}

		clientGameObject.Destroy();
		_clientMap.Remove( channel );
	}

	protected override void OnAwake()
	{
		ClientGroup = new GameObject( Scene.Root );
	}

	protected override void OnDestroy()
	{
		ClientGroup.Destroy();
	}
}
