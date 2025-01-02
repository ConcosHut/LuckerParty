using LuckerParty.UI;

namespace LuckerParty;

public sealed class LobbyManager : Component
{
	public const string READY = "Ready";
	private PanelComponent _panelComponent;

	protected override void OnEnabled()
	{
		var screenPanel = Scene.Directory.FindByName( "UI Root" ).First();
		_panelComponent = screenPanel.GetComponent<Lobby>() 
		                  ?? screenPanel.AddComponent<Lobby>();
	}

	protected override void OnDisabled()
	{
		Log.Info("Destroying panel component: " + _panelComponent  );
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

	[Rpc.Broadcast]
	public void ReadyUp( Client client )
	{
		// when we implement the Player component, we will probably want to
		// put the stuff on the player gameobject
		if ( !Networking.IsHost || Rpc.CallerId != client.ConnectionId) return;
		Log.Info("Toggling ready");
		ToggleReady(client.GameObject);
	}

	private void ToggleReady( GameObject gameObject )
	{
		if ( GameObject.Tags.Has( READY ) )
		{
			Log.Info( "Removing ready"  );
			GameObject.Tags.Remove( READY );
		}
		else
		{
			Log.Info( "Adding ready"  );
			GameObject.Tags.Add( READY );
		}
	}
	
	public interface ILobbyEvent : ISceneEvent<ILobbyEvent>
	{
		void OnStartGame( RoundConfiguration roundConfiguration );
	}
}
