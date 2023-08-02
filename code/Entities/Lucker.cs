using LuckerGame.Components.Lucker.Cameras;
using LuckerGame.Events;
using Sandbox;

namespace LuckerGame.Entities;

/// <summary>
/// Represents a Player.
/// This could belong to a Client or a Bot and represents a common entity to operate on for games and keeping score
/// </summary>
public partial class Lucker : Entity
{
	/// <summary>
	/// The entity this Player currently controls
	/// </summary>
	public Entity Pawn { get; set; }

	/// <summary>
	/// Before the round has started, this player indicated they were ready
	/// </summary>
	[Net] public bool Ready { get; set; }
	
	/// <summary>
	/// This Lucker's camera
	/// </summary>
	[BindComponent] public AbstractCamera Camera { get; }

	/// <summary>
	/// Creates and properly sets up a Player entity for a given client
	/// </summary>
	/// <param name="client">the client to own the player</param>
	/// <returns>the newly created player</returns>
	public static Lucker CreateLuckerForClient( IClient client )
	{
		var player = new Lucker();
		client.Pawn = player;
		player.Owner = client as Entity;
		player.Name = client.Name;
		var camera = player.Components.Create<RTSCamera>();

		return player;
	}

	/// <summary>
	/// Allows clients to request setting their ready state
	/// </summary>
	/// <param name="readyState">whether they are readying up or calming down</param>
	[ConCmd.Server("set_ready")]
	public static void ReadyUpCommand(bool readyState)
	{
		var client = ConsoleSystem.Caller;
		var player = client.Pawn as Lucker;
		player.SetReady( readyState );
	}

	/// <summary>
	/// Sets this player's ready state
	/// </summary>
	/// <param name="ready">the ready state being set</param>
	public void SetReady(bool ready)
	{
		Ready = ready;
		if ( Game.IsServer )
		{
			Event.Run( LuckerEvent.PlayerReady, this, ready );
		}
	}

	/// <inheritdoc/>
	public override void Simulate( IClient cl )
	{
		base.Simulate( cl );
		
	}

	public override void FrameSimulate( IClient cl )
	{
		base.FrameSimulate( cl );
		Camera?.Update();
	}

	public override void BuildInput()
	{
		base.BuildInput();
		Camera?.BuildInput();
	}
}
