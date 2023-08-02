using System;
using System.Collections.Generic;
using LuckerGame.Entities;
using Sandbox;

namespace LuckerGame.Minigames;

public abstract class Minigame : Entity
{
	public abstract string Name { get; }
	/// <summary>
	/// Initializes the minigame with a list of luckers playing it
	/// </summary>
	/// <param name="players">the players who made it into the minigame</param>
	public abstract void Initialize(List<Lucker> players);
	
	/// <summary>
	/// Cleans up any entities and components created by this minigame.
	/// It is not necessary to remove the lucker's pawns, the manager will do so if any were assigned.
	/// </summary>
	public abstract void Cleanup();
}
