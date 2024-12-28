using System;

namespace LuckerParty;

/// <summary>
///     Manages the Game state, which right now is either Lobby or Round.
///     Maintains the lifecycle of the Lobby and Round Managers.
/// </summary>
public sealed class GameManager : Component
{
	public enum State
	{
		Lobby,
		Round
	}

	private Component _currentStateManager;

	public State CurrentState { get; private set; } = State.Lobby;

	protected override void OnStart()
	{
		// Start Lobby
		_currentStateManager = GameObject.GetComponent<LobbyManager>() 
		                       ?? GameObject.AddComponent<LobbyManager>();
	}

	public void Transition( State newState )
	{
		Log.Info(
			$"Transitioning to {newState}" );
		if ( newState == CurrentState )
		{
			return;
		}

		CurrentState = newState;
		_currentStateManager.Destroy();
		_currentStateManager = newState switch
		{
			State.Lobby => GameObject.AddComponent<LobbyManager>(),
			State.Round => GameObject.AddComponent<RoundManager>(),
			_ => throw new ArgumentOutOfRangeException( nameof(newState), newState, null )
		};
	}
}
