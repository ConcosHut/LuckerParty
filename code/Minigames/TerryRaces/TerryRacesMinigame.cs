using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using LuckerGame.Components.Lucker.Cameras;
using LuckerGame.Components.Pawn;
using LuckerGame.Entities;
using Sandbox;
using Sandbox.UI;

namespace LuckerGame.Minigames.TerryRaces;

[Library( "mg_terry_races" )]
public class TerryRaces : Minigame
{
	public override string Name => "Terry Races";
	private Random random = new Random();
	private Racer WinningRacer = null;
	private FixedCamera Camera;
	private List<Lucker> Players { get; set; }
	private List<Racer> Racers { get; set; } = new List<Racer>();
	private List<string> RacerNames = new List<string>
	{
		"Terrance",
		"Terrie",
		"TearBear",
		"Theresa"
	};
	private float StartingY = 290f;
	private float FinishLineY = -300f;
	private float StartingX = 90f;
	private float RacerXOffset = 60f;
	private float MinimumSpeed = .90f;
	private float MaximumSpeed = 1.1f;

	public override void Initialize( List<Lucker> players )
	{
		Players = players;

		// Setup cameras for players
		Players.ForEach( player =>
		{
			Camera = player.Components.Create<FixedCamera>();
			Camera.LookAt( new Vector3(0f, 0f, 400f), Rotation.FromPitch( -270 ) );
			Camera.FieldOfViewValue = 100f;
		} );

		SpawnRacers();
	}

	public override void Tick()
	{
		if ( Racers != null )
		{
			Racers.ForEach( racer =>
			{
				if ( WinningRacer == null )
				{
					// Log.Info( $"{racer.Name} is racing" );
					GetWinningRacer();
					racer.ContinueRacing();
				}
				else
				{
					// Log.Info( $"{racer.Name} is stopping." );
					racer.StopRacing();
				}
			} );
		}
	}

	public override void Cleanup()
	{
	}

	private void SpawnRacers()
	{
		for ( int i = 0; i < RacerNames.Count; i++ )
		{
			var clothing = new ClothingContainer();
			var racer = new Racer();
			var x = StartingX - RacerXOffset * i;

			clothing.Toggle( GetRandomBottomClothing() );
			clothing.Toggle( GetRandomHatClothing() );
			clothing.DressEntity( racer );

			racer.Name = RacerNames[i];
			racer.Position = new Vector3( x, StartingY, 0 );
			racer.Rotation = Rotation.FromYaw( -90 );
			racer.GenerateSpeed( random, MinimumSpeed, MaximumSpeed );

			Racers.Add( racer );
		}
	}

	private Clothing GetRandomBottomClothing()
	{
		return ResourceLibrary
			.GetAll<Clothing>()
			.Where( c => c.Category == Clothing.ClothingCategory.Bottoms )
			.OrderBy( _ => Guid.NewGuid() )
			.FirstOrDefault();
	}

	private Clothing GetRandomHatClothing()
	{
		return ResourceLibrary
			.GetAll<Clothing>()
			.Where( c => c.Category == Clothing.ClothingCategory.Hat )
			.OrderBy( _ => Guid.NewGuid() )
			.FirstOrDefault();
	}

	private void GetWinningRacer()
	{
		foreach ( Racer racer in Racers )
		{
			if ( racer.Position.y <= FinishLineY )
			{
				WinningRacer = racer;
				Log.Info( $"Winning racer: {racer.Name}" );
			}
		}
	}
}
