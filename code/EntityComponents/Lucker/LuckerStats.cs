using Sandbox;
using Sandbox.UI;

namespace LuckerGame.EntityComponents.Lucker;

/// <summary>
/// Handles the stats associated with a lucker during a lobby.
/// </summary>
public partial class LuckerStats : EntityComponent<Entities.Lucker>, ISingletonComponent
{
	/// <summary>
	/// The lucker's current score.
	/// </summary>
	[Net] private long Score { get; set; }

	/// <summary>
	/// Adds points to this lucker's score
	/// </summary>
	/// <param name="points">points to add (or remove if negative)</param>
	public void AddScore( long points )
	{
		Score += points;
		if ( points == 0 )
		{
			return;
		}

		var message = $"{Entity.Name} {(points > 0 ? "gained" : "lost")} {points} points!";
		ChatBox.AddInformation( To.Everyone, message );
	}

	/// <summary>
	/// Resets this lucker's score to zero
	/// </summary>
	public void ResetScore()
	{
		Score = 0;
	}

	/// <summary>
	/// Gets this lucker's current score
	/// </summary>
	/// <returns>this lucker's current score</returns>
	public long GetScore()
	{
		return Score;
	}
}
