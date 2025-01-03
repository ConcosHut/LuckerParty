using System;

namespace LuckerParty;

/// <summary>
///     A Lucker Party Client. Entry point for a user (or bot?) to get a UI and become a Spectator or Lucker after they've
///     connected to the lobby.
/// </summary>
public sealed class Client : Component
{
	public enum Type
	{
		None,
		Spectator,
		Player
	}

	public static Client Local => Game.ActiveScene.Components.GetAll<Client>().First( client => !client.IsProxy );

	[Sync] public Guid ConnectionId { get; set; }
	public Connection Connection => Connection.Find( ConnectionId );
	public string Name => Connection.DisplayName;
	public DateTimeOffset ConnectionTime => Connection.ConnectionTime;

	public Type CurrentType =>
		Components.Get<Player>() != null ? Type.Player :
		Components.Get<Spectator>() != null ? Type.Spectator : Type.None;

	[Rpc.Host]
	public void BecomePlayer()
	{
		// Ignore request if:
		// 1. The requester is not the host nor Client owner
		// 2. The Client is already a Player
		if ( !(Networking.IsHost || Rpc.CallerId == ConnectionId) || CurrentType == Type.Player )
		{
			return;
		}

		Log.Info( $"{Name} Becoming player" );
		if ( CurrentType == Type.Spectator )
		{
			Components.Get<Spectator>().Destroy();
		}

		GetOrAddComponent<Player>();
		Network.Refresh();
	}

	[Rpc.Host]
	public void BecomeSpectator()
	{
		// Ignore request if:
		// 1. The requester is not the host nor Client owner
		// 2. The Client is already a Spectator
		if ( !(Networking.IsHost || Rpc.CallerId == ConnectionId) || CurrentType == Type.Spectator )
		{
			return;
		}

		if ( CurrentType == Type.Player )
		{
			Components.Get<Player>().Destroy();
		}

		GetOrAddComponent<Spectator>();
		Network.Refresh();
	}
}
