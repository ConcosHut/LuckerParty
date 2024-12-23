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

	public State CurrentState { get; private set; } = State.Lobby;

	protected override void OnStart()
	{
		// Start Lobby
		GameObject.AddComponent<LobbyManager>();
	}
}
