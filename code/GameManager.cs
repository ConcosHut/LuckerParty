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

	[Sync( SyncFlags.FromHost )] public State CurrentState { get; private set; } = State.Lobby;

	protected override void OnEnabled()
	{
		if ( IsProxy )
		{
			return;
		}

		// Start Lobby
		_currentStateManager = GameObject.GetComponent<LobbyManager>()
		                       ?? GameObject.AddComponent<LobbyManager>();
		Network.Refresh();
	}

	/// <summary>
	///     Change to a new Game State
	/// </summary>
	/// <param name="newState">The new Game State to transition to</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when newState is invalid State enum value</exception>
	[Rpc.Host( NetFlags.HostOnly )]
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
		Network.Refresh();
	}
}
