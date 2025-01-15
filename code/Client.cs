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

	public static Client Local => Game.ActiveScene.Components.GetAll<Client>()
		.First( client => client.ConnectionId == Connection.Local.Id );

	[Sync] public Guid ConnectionId { get; set; }
	public Connection Connection => Connection.Find( ConnectionId );
	public string Name => Connection.DisplayName;
	public DateTimeOffset ConnectionTime => Connection.ConnectionTime;

	public Type CurrentType =>
		Components.Get<Player>() != null ? Type.Player :
		Components.Get<Spectator>() != null ? Type.Spectator : Type.None;

	/// <summary>
	///     Makes this Client a Player by adding a Player Component to the Client Object.
	///     Executed on the host, can only be called by the host or the client connection.
	/// </summary>
	[Rpc.Host]
	public void BecomePlayer()
	{
		// Ignore request if:
		// 1. The requester is not the host nor Client owner
		// 2. The Client is already a Player
		if ( !(Rpc.Caller.IsHost || Rpc.CallerId == ConnectionId) || CurrentType == Type.Player )
		{
			return;
		}

		if ( CurrentType == Type.Spectator )
		{
			Components.Get<Spectator>().Destroy();
		}

		Log.Info( $"{Name} becoming Player" );
		GetOrAddComponent<Player>();
		Network.Refresh();
	}

	/// <summary>
	///     Makes this Client a Spectator by adding a Spectator Component to the Client Object.
	///     Executed on the host, can only be called by the host or the client connection.
	/// </summary>
	[Rpc.Host]
	public void BecomeSpectator()
	{
		// Ignore request if:
		// 1. The requester is not the host nor Client owner
		// 2. The Client is already a Spectator
		if ( !(Rpc.Caller.IsHost || Rpc.CallerId == ConnectionId) || CurrentType == Type.Spectator )
		{
			return;
		}

		if ( CurrentType == Type.Player )
		{
			Components.Get<Player>().Destroy();
		}

		Log.Info( $"{Name} becoming Spectator" );
		GetOrAddComponent<Spectator>();
		Network.Refresh();
	}
}
