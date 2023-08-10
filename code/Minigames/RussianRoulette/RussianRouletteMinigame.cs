using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LuckerGame.Components.Lucker.Cameras;
using LuckerGame.Components.Pawn;
using LuckerGame.Entities;
using Sandbox;
using Sandbox.UI;

namespace LuckerGame.Minigames.RussianRoulette;

[Library("mg_russian_roulette")]
public class RussianRouletteMinigame : Minigame
{
	public override string Name => "Russian Roulette";
	private List<Lucker> Players { get; set; }
	private Entities.Pawn Shooter { get; set; }
	private const float ShooterDistance = 80f;
	private const float TimeBetweenShots = 7f;
	private int Taunted = 0;

	private List<Entities.Pawn> DeadVictims => Players
		.Select( player => player.Pawn as Entities.Pawn )
		.Where( pawn => !pawn.IsValid || pawn.LifeState != LifeState.Alive )
		.ToList();

	private TimeSince TimeSinceShot { get; set; }

	public override void Initialize( List<Lucker> players )
	{
		Players = players;
		Shooter = new Entities.Pawn();
		var shooterInventory = Shooter.Components.Create<PawnInventory>();
		shooterInventory.AddWeapon( new RussianPistol() );

		// Setup cameras for players
		Players.ForEach( player =>
		{
			player.Components.Create<RTSCamera>();
			player.Position = Shooter.Position;
		} );

		Players.Select((player, i) => (Player: player, Index: i)  ).ToList().ForEach( pair =>
		{
			var player = pair.Player;
			var index = pair.Index;
			var pawn = new Entities.Pawn();
			pawn.Name = player.Name;
			pawn.Tags.Add( "victim" );
			pawn.Health = 1;
			player.Pawn = pawn;
			pawn.DressFromClient( player.Client );

			var pawnOffset = ShooterDistance * (index % 2 == 0 ? Vector3.Forward : Vector3.Right) * (index % 4 >= 2 ? -1 : 1);
			
			player.Pawn.Position = Shooter.Position + pawnOffset;
			pawn.LookAt(Shooter.Position);
		} );
		TimeSinceShot = 0;
	}

	public override void Tick()
	{
		if ( DeadVictims.Any() )
		{
			if ( Taunted != int.MaxValue )
			{
				ChatBox.AddChatEntry( To.Everyone, "Shooter", "Heh, nothing personnel, kid." );
				Taunted = int.MaxValue;
			}
			return;
		}
		if ( TimeSinceShot > TimeBetweenShots )
		{
			TimeSinceShot = 0;
			Taunted = 0;
			Shooter.Inventory.ActiveWeapon.PrimaryAttack();
			if ( !DeadVictims.Any() )
			{
				ChatBox.AddChatEntry( To.Everyone, "Shooter", "Fucking lag..." );
			}
		}
		else if ( TimeSinceShot > TimeBetweenShots * .8f && Taunted == 1)
		{
			var victim = Players.Select( player => player.Pawn as Racer )
				.OrderBy( _ => Guid.NewGuid() )
				.FirstOrDefault();
			Shooter.LookAt( victim.Position );
			ChatBox.AddChatEntry( To.Everyone, "Shooter", $"I'm gonna eat you up, {victim.Name}" );
			Taunted++;
		}
		else if ( TimeSinceShot > TimeBetweenShots / 2  && Taunted == 0)
		{
			ChatBox.AddChatEntry( To.Everyone, "Shooter", "Im gettin' ready!" );
			Taunted++;
		}
	}

	public override void Cleanup()
	{
		Shooter?.Delete();
	}
}
