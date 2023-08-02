using Sandbox;

namespace LuckerGame.Events;

public static partial class LuckerEvent
{
	public const string MinigameEnd = "lucker.minigameEnd";

	/// <summary>
	/// Event is run on the server whenever a minigame ends
	/// </summary>
	public class MinigameEndAttribute : EventAttribute
	{
		public MinigameEndAttribute() : base(MinigameEnd)
		{
		}
	}
}
