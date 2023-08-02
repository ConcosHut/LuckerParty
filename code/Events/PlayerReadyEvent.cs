using Sandbox;

namespace LuckerGame.Events;

public static partial class LuckerEvent
{
	public const string PlayerReady = "lucker.playerReady";

	/// <summary>
	/// Event is run on the server whenever a player changes ready state
	/// The event handler is given the player that readied up and their new ready state
	/// </summary>
	[MethodArguments(typeof(Entities.Lucker), typeof(bool))]
	public class PlayerReadyAttribute : EventAttribute
	{
		public PlayerReadyAttribute() : base(PlayerReady)
		{
		}
	}
}
