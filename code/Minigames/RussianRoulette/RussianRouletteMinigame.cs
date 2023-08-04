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
	private Pawn Shooter { get; set; }
	
	private const float ShooterDistance = 80f;
	private const float TimeBetweenShots = 7f;
	private const float TimeBetweenDeathAndEnd = 5f;
	private const string ShooterName = "The Russian";
	private int Taunted = 0;
	private Pawn ShooterTarget;

	private List<Pawn> DeadVictims => Players
		.Select( player => player.Pawn as Pawn )
		.Where( pawn => pawn is not { IsValid: true } || pawn.LifeState != LifeState.Alive )
		.ToList();

	private TimeSince TimeSinceShot { get; set; }
	private TimeSince TimeSinceDeadVictim { get; set; }

	public override void Initialize( List<Lucker> players )
	{
		Players = players;
		Shooter = new Pawn();
		Shooter.Name = ShooterName;
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
			var pawn = new Pawn();
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
		Taunted = 0;
	}

	public override bool Tick()
	{
		// Someone is dead, we're getting ready to end
		if ( DeadVictims.Any() )
		{
			if ( Taunted != int.MaxValue )
			{
				ChatBox.AddChatEntry( To.Everyone, Shooter.Name, "Heh, nothing personnel, kid." );
				Taunted = int.MaxValue;
				TimeSinceDeadVictim = 0;
			}
			else if(TimeSinceDeadVictim > TimeBetweenDeathAndEnd)
			{
				return true;
			}
		}
		else if ( TimeSinceShot > TimeBetweenShots )
		{
			TimeSinceShot = 0;
			Taunted = 0;
			Shooter.Inventory.ActiveWeapon.PrimaryAttack();
			if ( !DeadVictims.Any() )
			{
				ChatBox.AddChatEntry( To.Everyone, Shooter.Name, "Fucking lag..." );
			}
		}
		else if ( TimeSinceShot > TimeBetweenShots * .8f && Taunted == 1)
		{
			ShooterTarget = Players.Select( player => player.Pawn as Pawn )
				.OrderBy( _ => Guid.NewGuid() )
				.FirstOrDefault();
			Shooter.LookAt( ShooterTarget.Position );
			var chance = 1f / Shooter.Inventory.ActiveWeapon.Ammo;
			ChatBox.AddChatEntry( To.Everyone, Shooter.Name, $"Good luck, {ShooterTarget.Name}! You have a {chance:P0} chance to die!" );
			Taunted++;
		}
		else if ( TimeSinceShot > TimeBetweenShots / 2  && Taunted == 0)
		{
			ChatBox.AddChatEntry( To.Everyone, Shooter.Name, "Im gettin' ready!" );
			Taunted++;
		}

		return false;
	}

	public override void Cleanup()
	{
		Shooter?.Delete();
	}
}
