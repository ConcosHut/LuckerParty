using System;
using System.Collections.Generic;
using System.Linq;
using LuckerGame.Entities;
using Sandbox;

namespace LuckerGame.Minigames.RussianRoulette;

public class RussianRouletteMinigame : Minigame
{
	public override string Name => "Russian Roulette";

	public override void Initialize( List<Lucker> players )
	{
		var pawn = new Pawn();

		// Get all of the spawnpoints
		var spawnpoints = Entity.All.OfType<SpawnPoint>();

		// chose a random one
		var randomSpawnPoint = spawnpoints.OrderBy( x => Guid.NewGuid() ).FirstOrDefault();

		// if it exists, place the pawn there
		if ( randomSpawnPoint != null )
		{
			var tx = randomSpawnPoint.Transform;
			tx.Position = tx.Position + Vector3.Up * 50.0f; // raise it up
			pawn.Position = tx.Position;
		}
	}

	public override void Cleanup()
	{
		throw new System.NotImplementedException();
	}
}
