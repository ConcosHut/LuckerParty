using LuckerGame.Components.Lucker.Cameras;
using LuckerGame.EntityComponents.Lucker;
using LuckerGame.Events;
using Sandbox;

namespace LuckerGame.Entities;

/// <summary>
/// Represents a person playing the game.
/// This could belong to a Client or a Bot and represents a common entity to operate on for games and keeping score
/// </summary>
public partial class Lucker : Entity
{
	/// <summary>
	/// The entity this lucker controls. This value is networked and should be accessed through <see cref="Pawn"/>.
	/// </summary>
	[Net] private Entity InternalPawn { get; set; }

	/// <summary>
	/// Accesses or sets the entity this lucker currently controls
	/// </summary>
	public Entity Pawn
	{
		get => InternalPawn;
		set
		{
			InternalPawn = value;
			if ( value != null )
			{
				value.Owner = this;
			}
		}
	}

	/// <summary>
	/// Before the round has started, this lucker indicated they were ready
	/// </summary>
	[Net] public bool Ready { get; set; }
	
	/// <summary>
	/// This Lucker's camera
	/// </summary>
	[BindComponent] public AbstractCamera Camera { get; }
	
	[BindComponent] public LuckerStats Stats { get; }

	/// <summary>
	/// Creates and properly sets up a <see cref="Lucker"/> entity for a given client
	/// </summary>
	/// <param name="client">the client to own the lucker</param>
	/// <returns>the newly created lucker</returns>
	public static Lucker CreateLuckerForClient( IClient client )
	{
		var lucker = new Lucker();
		client.Pawn = lucker;
		lucker.Owner = client as Entity;
		lucker.Name = client.Name;
		lucker.Components.Create<RTSCamera>();
		lucker.Components.Create<LuckerStats>();

		return lucker;
	}

	/// <summary>
	/// Allows clients to request setting their ready state
	/// </summary>
	/// <param name="readyState">whether they are readying up or calming down</param>
	[ConCmd.Server("set_ready")]
	public static void ReadyUpCommand(bool readyState)
	{
		var client = ConsoleSystem.Caller;
		var lucker = client.Pawn as Lucker;
		lucker.SetReady( readyState );
	}

	/// <summary>
	/// Sets this lucker's ready state
	/// </summary>
	/// <param name="ready">the ready state being set</param>
	public void SetReady(bool ready)
	{
		Ready = ready;
		if ( Game.IsServer )
		{
			Event.Run( LuckerEvent.LuckerReady, this, ready );
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
