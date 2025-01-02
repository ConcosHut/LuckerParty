using System;

namespace LuckerParty;

/// <summary>
///     A Lucker Party Client. Entry point for a user (or bot?) to get a UI and become a Spectator or Lucker after they've
///     connected to the lobby.
/// </summary>
public sealed class Client : Component
{
	[Sync] public Guid ConnectionId { get; set; }
	public Connection Connection => Connection.Find( ConnectionId );
	public string Name => Connection.DisplayName;
	public DateTimeOffset ConnectionTime => Connection.ConnectionTime;
}
