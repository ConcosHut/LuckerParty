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
	private GameObject _clientGroup;

	public IEnumerable<Client> Clients => _clientGroup.GetComponentsInChildren<Client>();

	public void OnActive( Connection channel )
	{
		// Set up the Client GameObject
		var gameObject = new GameObject( _clientGroup ) { Name = $"{channel.DisplayName} ({channel.SteamId})" };
		_clientMap.Add( channel, gameObject );
		var client = gameObject.AddComponent<Client>();
		client.ConnectionId = channel.Id;

		// Spawn it on remote clients
		gameObject.NetworkSpawn( channel );

		IClientEvent.Post( e => e.OnConnected( client ) );
	}

	public void OnDisconnected( Connection channel )
	{
		if ( !_clientMap.TryGetValue( channel, out var clientGameObject ) )
		{
			Log.Warning( $"Disconnected client {channel.SteamId} has no associated GameObject." );
			return;
		}

		var client = clientGameObject.GetComponent<Client>();
		IClientEvent.Post( e => e.OnDisconnected( client ) );

		clientGameObject.Destroy();
		_clientMap.Remove( channel );
	}

	protected override void OnAwake()
	{
		_clientGroup = new GameObject( Scene.Root ) { Name = "Clients", NetworkMode = NetworkMode.Object };
	}

	protected override void OnDestroy()
	{
		_clientGroup.Destroy();
	}

	public interface IClientEvent : ISceneEvent<IClientEvent>
	{
		void OnConnected( Client client )
		{
		}

		void OnDisconnected( Client client ) { }
	}
}
