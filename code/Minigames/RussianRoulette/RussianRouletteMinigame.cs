using System;
using System.Collections.Generic;
using System.Linq;
using LuckerGame.Components.Lucker.Cameras;
using LuckerGame.Entities;
using Sandbox;

namespace LuckerGame.Minigames.RussianRoulette;

public class RussianRouletteMinigame : Minigame
{
	public override string Name => "Russian Roulette";
	private List<Lucker> Players { get; set; }
	private Pawn Shooter { get; set; }
	private const float ShooterDistance = 50f;
	private const int CardinalDirections = 4;

	public override void Initialize( List<Lucker> players )
	{
		Players = players;
		Shooter = new Pawn();

		/*// Spawn shooter at a random spawnpoint
		var spawnpoints = Entity.All.OfType<SpawnPoint>();
		var randomSpawnPoint = spawnpoints.OrderBy( x => Guid.NewGuid() ).FirstOrDefault();
		var tx = randomSpawnPoint.Transform;
		tx.Position = tx.Position + Vector3.Up * 50.0f; // raise it up
		Shooter.Position = tx.Position;*/

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
			player.Pawn = pawn;
			pawn.DressFromClient( player.Client );

			var pawnOffset = ShooterDistance * (index % 2 == 0 ? Vector3.Forward : Vector3.Right) * (index % 4 >= 2 ? -1 : 1);
			
			player.Pawn.Position = Shooter.Position + pawnOffset;
		} );
	}

	public override void Tick()
	{
		return;
	}

	public override void Cleanup()
	{
		Shooter?.Delete();
	}
}
