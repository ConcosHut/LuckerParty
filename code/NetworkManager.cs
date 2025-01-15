using System;

namespace LuckerParty;

/// <summary>
///     Listens to Network events and creates Client GameObjects
/// </summary>
public sealed class NetworkManager : Component, Component.INetworkListener
{
	/// <summary>
	///     A GameObject used for organizational grouping of Clients
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	private GameObject ClientGroup { get; set; }

	public IEnumerable<Client> Clients => ClientGroup.GetComponentsInChildren<Client>();

	public void OnConnected( Connection channel )
	{
		// Disallow spawning Networked Objects on the client. We are host-authoritative.
		channel.CanSpawnObjects = false;

		// Set up the Client GameObject
		var gameObject = new GameObject( ClientGroup ) { Name = $"{channel.DisplayName} ({channel.SteamId})" };
		var client = gameObject.AddComponent<Client>();
		client.ConnectionId = channel.Id;

		Log.Info( $"NetworkManager::OnConnected: {channel.DisplayName} {channel.Id}" );

		// Spawn it on remote clients
		gameObject.NetworkSpawn();

		IClientEvent.Post( e => e.OnConnected( client ) );
	}

	public void OnDisconnected( Connection channel )
	{
		var clientGameObject = TryFindClientByConnectionId( channel.Id );
		if ( clientGameObject == null )
		{
			Log.Warning( $"Disconnected client {channel.SteamId} has no associated GameObject." );
			return;
		}

		var client = clientGameObject.GetComponent<Client>();
		IClientEvent.Post( e => e.OnDisconnected( client ) );

		clientGameObject.Destroy();
	}

	private Client TryFindClientByConnectionId( Guid connectionId )
	{
		return ClientGroup.GetComponentsInChildren<Client>()
			.FirstOrDefault( client => client.ConnectionId == connectionId );
	}

	protected override void OnAwake()
	{
		ClientGroup = new GameObject( Scene.Root ) { Name = "Clients" };

		// Hack: Component.INetworkListener.OnConnected does not get called for the host, so fake it here.
		if ( !IsProxy )
		{
			OnConnected( Connection.Local );
		}
	}

	protected override void OnDestroy()
	{
		ClientGroup.Destroy();
	}

	public interface IClientEvent : ISceneEvent<IClientEvent>
	{
		void OnConnected( Client client )
		{
		}

		void OnDisconnected( Client client ) { }
	}
}
