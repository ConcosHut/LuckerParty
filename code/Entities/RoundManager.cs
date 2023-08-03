using System;
using System.Collections.Generic;
using System.Linq;
using LuckerGame.Enums;
using LuckerGame.Events;
using Sandbox;
using Sandbox.UI;

namespace LuckerGame.Entities;

/// <summary>
/// Manages rounds. Starting, stopping, triggering minigames from the manager, etc
/// </summary>
public partial class RoundManager : Entity
{
	/// <summary>
	/// The minigame manager we should be using to manage minigames
	/// </summary>
	private MinigameManager MinigameManager { get; set; }

	/// <summary>
	/// This percentage of the lobby must be ready to start the countdown for round start
	/// </summary>
	private const float RequiredReadyPercent = .5f;

	/// <summary>
	/// The number of seconds from the timer starting before the round starts
	/// </summary>
	private const float RoundStartCountdownSeconds = 5f;

	/// <summary>
	/// The state of the current round
	/// </summary>
	[Net] public RoundState RoundState { get; private set; }

	#region Countdown State
	/// <summary>
	/// How long since we started counting down to game start. Only useful if in the StartCountdown state.
	/// </summary>
	[Net] public TimeSince TimeSinceCountdownStarted { get; set; }

	/// <summary>
	/// The number of seconds left in the countdown. Only useful if in the StartCountdown state.
	/// </summary>
	public int SecondsLeftInCountdown => (int)Math.Ceiling( RoundStartCountdownSeconds - TimeSinceCountdownStarted );
	#endregion

	#region In Progress State

	private const int MinigamesPerRound = 1;

	private int MinigamesLeftInRound { get; set; }

	private List<Lucker> Players { get; set; }
	#endregion

	/// <inheritdoc/>
	public override void Spawn()
	{
		base.Spawn();
		RoundState = RoundState.NotStarted;
		MinigameManager = new MinigameManager();
	}

	/// <summary>
	/// Fires once per server tick
	/// </summary>
	[GameEvent.Tick.Server]
	public void Tick()
	{
		if ( RoundState == RoundState.StartCountdown && TimeSinceCountdownStarted > RoundStartCountdownSeconds )
		{
			Log.Info( "Starting round" );
			StartRound();
		}

		if ( RoundState == RoundState.InProgress )
		{
			MinigameManager.Tick();
		}
	}

	/// <summary>
	/// Is triggered whenever a player readies up
	/// </summary>
	/// <param name="readyLucker">the player that readied up, discarded</param>
	[LuckerEvent.PlayerReady]
	public void HandlePlayerReady( Lucker readyLucker, bool ready )
	{
		if ( RoundState != RoundState.NotStarted && RoundState != RoundState.StartCountdown )
		{
			return;
		}
		Log.Info( $"{readyLucker.Client.Name} set ready to {ready}" );
		var message = $"{readyLucker.Client.Name} is {(ready ? "now ready." : "no longer ready.")}";
		ChatBox.AddInformation( To.Everyone, message );
		var players = All.OfType<Lucker>().ToList();
		var readiedCount = players.Count( player => player.Ready );
		var totalCount = players.Count;
		if ( (float)readiedCount / totalCount > RequiredReadyPercent && RoundState == RoundState.NotStarted )
		{
			Log.Info( "Countdown started" );
			RoundState = RoundState.StartCountdown;
			TimeSinceCountdownStarted = 0;
		}
		else if ( (float)readiedCount / totalCount <= RequiredReadyPercent && RoundState == RoundState.StartCountdown )
		{
			Log.Info( "Countdown ended" );
			RoundState = RoundState.NotStarted;
		}
	}

	private void StartRound( string minigameName = null )
	{
		if ( RoundState == RoundState.InProgress )
		{
			Log.Warning( "Attempted to start round while one was in progress" );
			return;
		}

		RoundState = RoundState.InProgress;
		Players = All.OfType<Lucker>().ToList();

		if ( string.IsNullOrEmpty( minigameName ) )
		{
			MinigameManager.StartMinigame( Players );
		}
		else
		{
			MinigameManager.StartMinigame( Players, minigameName );
		}
	}

	[ConCmd.Server( "start_round" )]
	public static void ForceStart( string minigameName = null )
	{
		Entity.All.OfType<RoundManager>().FirstOrDefault()?.StartRound( minigameName );
	}
}
