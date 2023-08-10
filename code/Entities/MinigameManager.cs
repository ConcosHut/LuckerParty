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
	/// <summary>
	/// The currently loaded minigame
	/// </summary>
	[Net] public Minigame LoadedMinigame { get; private set; }
	
	/// <summary>
	/// A cached list of available minigames. Gets reloaded on a hotreload
	/// </summary>
	private List<TypeDescription> AvailableMinigames { get; set; }
	
	/// <summary>
	/// The luckers involved in the current minigame
	/// </summary>
	private List<Lucker> InvolvedLuckers { get; set; }

	public override void Spawn()
	{
		base.Spawn();
		FindMinigames();
	}

	public void StartMinigame(List<Lucker> luckers, string minigameName = null)
	{
		InvolvedLuckers = luckers.ToList();
		if (CheckForMinigames())
		{
			LoadedMinigame = string.IsNullOrEmpty( minigameName )
				? TypeLibrary.Create<Minigame>( AvailableMinigames.OrderBy( _ => Guid.NewGuid() ).FirstOrDefault()
					.TargetType )
				: TypeLibrary.Create<Minigame>( minigameName );
			ChatBox.AddInformation( To.Everyone, $"Starting {LoadedMinigame.Name}" );
			LoadedMinigame.Initialize( luckers );
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
			.ToList();
	}

	[Event.Hotload]
	private void Reload()
	{
		FindMinigames();
	}

	/// <summary>
	/// Goes through the luckers included in the loaded minigame and deletes and nulls out any pawns assigned to them
	/// </summary>
	private void CleanupLuckerPawns()
	{
		if ( LoadedMinigame is not { IsValid: true } || InvolvedLuckers == null)
		{
			Log.Warning( "Attempted to clean up players without a minigame loaded!" );
			return;
		}
		InvolvedLuckers.ForEach( lucker =>
		{
			lucker.Pawn?.Delete();
			lucker.Pawn = null;
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
		EndMinigame();
		return true;
	}

	private void EndMinigame()
	{
		LoadedMinigame.Cleanup();
		CleanupLuckerPawns();
		LoadedMinigame.Delete();
		LoadedMinigame = null;
		InvolvedLuckers = null;
	}
}
