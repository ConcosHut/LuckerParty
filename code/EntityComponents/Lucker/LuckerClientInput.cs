using Sandbox;

namespace LuckerGame.EntityComponents.Lucker;

/// <summary>
/// A component for capturing and passing around a client's input for an attached Lucker
/// </summary>
public class LuckerClientInput : EntityComponent<Entities.Lucker>
{
	[ClientInput]
	public Vector3 InputDirection { get; set; }
	
	[ClientInput]
	public Angles ViewAngles { get; set; }
}
