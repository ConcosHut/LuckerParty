using LuckerParty.UI;

namespace LuckerParty;

public sealed class LobbyManager : Component
{
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

	public interface ILobbyEvent : ISceneEvent<ILobbyEvent>
	{
		void OnStartGame( RoundConfiguration roundConfiguration );
	}
}
