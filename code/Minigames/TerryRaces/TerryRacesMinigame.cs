using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
	private List<Lucker> Players { get; set; }

	public override void Initialize( List<Lucker> players )
	{
		Players = players;
		// Setup cameras for players
		Players.ForEach( player =>
		{
			player.Components.Create<RTSCamera>();
		} );

		Players.Select( ( player, i ) => (Player: player, Index: i) ).ToList().ForEach( pair =>
		{
			var player = pair.Player;
			var index = pair.Index;
			var pawn = new Pawn();
			pawn.Name = player.Name;
			pawn.Tags.Add( "victim" );
			pawn.Health = 1;
			player.Pawn = pawn;
			pawn.DressFromClient( player.Client );

		} );
	}

	public override void Tick()
	{

	}

	public override void Cleanup()
	{
	}
}
