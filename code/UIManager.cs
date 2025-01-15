using System.Threading.Tasks;

namespace LuckerParty;

/// <summary>
///     Just spawns the non-networked UI Root prefab that has a Screen Panel on it. This is useful so that the Host doesn't
///     send its own UI to the Client on connection in the Scene Snapshot. The UI Root is parented to the Singletons Group.
/// </summary>
public class UiManager : Component
{
	public GameObject UiRoot { get; private set; }
	[Property] private GameObject UiRootPrefab { get; set; }

	protected override Task OnLoad()
	{
		UiRoot = UiRootPrefab.Clone();
		UiRoot.BreakFromPrefab();
		UiRoot.Parent = GameObject.Parent;
		return Task.CompletedTask;
	}
}
