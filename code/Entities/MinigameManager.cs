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

	public override void Spawn()
	{
		base.Spawn();
		FindMinigames();
	}

	public void StartRandomMinigame(List<Lucker> players)
	{
		if ( (AvailableMinigames?.Count ?? 0) == 0 )
		{
			Log.Error( "Attempted to start minigame, but none available" );
			return;
		}

		LoadedMinigame = AvailableMinigames.OrderBy( _ => Guid.NewGuid() ).FirstOrDefault();
		ChatBox.AddInformation( To.Everyone, $"Starting {LoadedMinigame.Name}" );
		LoadedMinigame.Initialize( players );
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
}
