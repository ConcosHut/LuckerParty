using Sandbox;

namespace LuckerGame.Events;

public static partial class LuckerEvent
{
	public const string LuckerReady = "lucker.luckerReady";

	/// <summary>
	/// Event is run on the server whenever a lucker changes ready state
	/// The event handler is given the lucker that readied up and their new ready state
	/// </summary>
	[MethodArguments(typeof(Entities.Lucker), typeof(bool))]
	public class LuckerReadyAttribute : EventAttribute
	{
		public LuckerReadyAttribute() : base(LuckerReady)
		{
		}
	}
}
