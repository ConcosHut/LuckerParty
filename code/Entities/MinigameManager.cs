using System;
using System.Collections.Generic;
using System.Linq;
using LuckerGame.Minigames;
using Sandbox;
using Sandbox.UI;

namespace LuckerGame.Entities;

/// <summary>
/// Manages minigames
/// </summary>
public partial class MinigameManager : Entity
{
	[Net] public Minigame LoadedMinigame { get; private set; }
	private List<Minigame> AvailableMinigames { get; set; }
	private List<Lucker> InvolvedPlayers { get; set; }

	public override void Spawn()
	{
		base.Spawn();
		FindMinigames();
	}

	public void StartMinigame(List<Lucker> players, string minigameName = null)
	{
		InvolvedPlayers = players.ToList();
		if (CheckForMinigames())
		{
			LoadedMinigame = string.IsNullOrEmpty( minigameName ) ? AvailableMinigames.OrderBy( _ => Guid.NewGuid() ).FirstOrDefault() : TypeLibrary.Create<Minigame>( minigameName );
			ChatBox.AddInformation( To.Everyone, $"Starting {LoadedMinigame.Name}" );
			LoadedMinigame.Initialize( players );
		}
	}

	private bool CheckForMinigames()
	{
		if ( (AvailableMinigames?.Count ?? 0) == 0 )
		{
			Log.Error( "Attempted to start minigame, but none available" );
			return false;
		}

		return true;
	}
	
	private void FindMinigames()
	{
		AvailableMinigames = TypeLibrary.GetTypes<Minigame>()
			.Where( type => !type.IsAbstract && !type.IsInterface )
			.Select( td => TypeLibrary.Create<Minigame>( td.TargetType ) ).ToList();
	}

	[Event.Hotload]
	private void Reload()
	{
		FindMinigames();
	}

	/// <summary>
	/// Goes through the players included in the loaded minigame and deletes and nulls out any pawns assigned to them
	/// </summary>
	private void CleanupPlayerPawns()
	{
		if ( LoadedMinigame is not { IsValid: true } || InvolvedPlayers == null)
		{
			Log.Warning( "Attempted to clean up players without a minigame loaded!" );
			return;
		}
		InvolvedPlayers.ForEach( player =>
		{
			player.Pawn.Delete();
			player.Pawn = null;
		} );
	}
	
	/// <summary>
	/// Called once per tick by the RoundManager. Ticks any running minigame.
	/// </summary>
	/// <returns>true if the current minigame has ended, else false</returns>
	public bool Tick()
	{
		if ( LoadedMinigame is not { IsValid: true } )
		{
			return false;
		}
		var ended = LoadedMinigame.Tick();
		if ( !ended )
		{
			return false;
		}

		LoadedMinigame.Cleanup();
		LoadedMinigame = null;
		return true;

	}
}
