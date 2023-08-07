using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LuckerGame.Components.Lucker.Cameras;
using LuckerGame.Components.Pawn;
using LuckerGame.Entities;
using Sandbox;
using Sandbox.Minigames.TerryRaces;
using Sandbox.UI;

namespace LuckerGame.Minigames.TerryRaces;

[Library( "mg_terry_races" )]
public class TerryRaces : Minigame
{
	public override string Name => "Terry Races";
	private List<Lucker> Players { get; set; }
	private FixedCamera Camera;
	private List<Racer> Racers { get; set; }
	private float StartingY = 290f;
	private float StartingX = 90f;
	private float RacerXOffset = 60f;
	private int NumberOfRacers = 4;

	public override void Initialize( List<Lucker> players )
	{
		Players = players;
		
		// Setup cameras for players
		Players.ForEach( player =>
		{
			Camera = player.Components.Create<FixedCamera>();
			Camera.LookAt( Constants.TOPDOWN_CAMERA_POSITION, Rotation.FromPitch( -270 ) );
			Camera.FieldOfViewValue = 100f;
		} );

		// SpawnRacers();
	}
	
	private void SpawnRacers()
	{
		for ( int i = 0; i < NumberOfRacers; i++ )
		{
			var racer = new Racer();
			var x = StartingX - RacerXOffset * i;
			racer.Position = new Vector3( x, StartingY, 0 );
			racer.Rotation = Rotation.FromPitch( -90 );
			Racers.Add( racer ); 
		}
	}

	public override void Tick()
	{
	}

	public override void Cleanup()
	{
	}
}
