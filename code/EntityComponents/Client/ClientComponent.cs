using Sandbox;

namespace LuckerGame.Components.Client;

/// <summary>
/// Intended to be a base class for components intended for IClient, as IClient cannot be used as an EntityComponent type param
/// </summary>
public abstract class ClientComponent : EntityComponent
{
	public IClient Client => Entity as IClient;
}
