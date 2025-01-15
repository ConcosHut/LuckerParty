using LuckerParty.UI;

namespace LuckerParty;

public sealed class LobbyManager : Component, NetworkManager.IClientEvent
{
	private PanelComponent _panelComponent;
	private UiManager UiManager => Scene.Components.GetInDescendantsOrSelf<UiManager>();

	protected override void OnEnabled()
	{
		Client.Local.BecomePlayer();
		// Show Lobby UI
		var uiRoot = UiManager.UiRoot;
		_panelComponent = uiRoot.GetComponent<Lobby>()
		                  ?? uiRoot.AddComponent<Lobby>();
	}

	protected override void OnDisabled()
	{
		Log.Info( "Destroying panel component: " + _panelComponent );
		_panelComponent.Destroy();
		_panelComponent = null;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Input.Released( "Menu" ) )
		{
			_panelComponent.Enabled = !_panelComponent.Enabled;
		}
	}

	[Rpc.Host]
	public void SetIsReady( Client client, bool isReady )
	{
		// Don't allow Clients to set other Clients' ready state
		if ( Rpc.CallerId != client.ConnectionId )
		{
			return;
		}

		if ( IsReady( client ) == isReady )
		{
			return;
		}

		Log.Info( $"Setting {client.Name} ready state to {isReady}" );
		if ( isReady )
		{
			client.AddComponent<LobbyReady>();
		}
		else
		{
			client.GetComponent<LobbyReady>().Destroy();
		}

		client.Network.Refresh();
	}

	public bool IsReady( Client client )
	{
		return client.Components.Get<LobbyReady>().IsValid();
	}

	public interface ILobbyEvent : ISceneEvent<ILobbyEvent>
	{
		void OnStartGame( RoundConfiguration roundConfiguration );
	}
}
