namespace LuckerGame.Enums;

/// <summary>
/// The state of a current round of minigames
/// </summary>
public enum RoundState
{
	/// <summary>
	/// This round has not yet started
	/// </summary>
	NotStarted,
	/// <summary>
	/// This round is counting down to start
	/// </summary>
	StartCountdown,
	/// <summary>
	/// This round is currently in progress
	/// </summary>
	InProgress
}
