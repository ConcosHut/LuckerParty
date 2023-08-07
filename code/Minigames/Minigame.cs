using System;
using System.Collections.Generic;
using LuckerGame.Entities;
using Sandbox;

namespace LuckerGame.Minigames;

public abstract class Minigame : Entity
{
	/// <summary>
	/// The name of this minigame
	/// </summary>
	public abstract string Name { get; }
	
	/// <summary>
	/// Initializes the minigame with a list of luckers playing it.
	/// </summary>
	/// <param name="luckers">the luckers who made it into the minigame</param>
	public abstract void Initialize(List<Lucker> luckers);

	/// <summary>
	/// Once a minigame is loaded and initialized, this method is called once per server tick.
	/// </summary>
	/// <returns>true if the minigame has ended, false otherwise</returns>
	public abstract bool Tick();
	
	/// <summary>
	/// Cleans up any entities and components created by this minigame.
	/// It is not necessary to remove the lucker's pawns, the manager will do so if any were assigned.
	/// </summary>
	public abstract void Cleanup();
}
