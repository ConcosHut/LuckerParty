using LuckerParty.UI;

namespace LuckerParty;

public sealed class LobbyManager : Component
{
	private PanelComponent _panelComponent;

	protected override void OnEnabled()
	{
		var screenPanel = Scene.Directory.FindByName( "UI Root" ).First();
		_panelComponent = screenPanel.AddComponent<Lobby>();
	}

	protected override void OnDisabled()
	{
		_panelComponent.Destroy();
		_panelComponent = null;
	}

	public interface ILobbyEvent : ISceneEvent<ILobbyEvent>
	{
		void OnStartGame( RoundConfiguration roundConfiguration );
	}
}
