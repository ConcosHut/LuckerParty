using LuckerGame.Entities;
using Sandbox;

namespace LuckerGame.Components.Lucker.Cameras;

public abstract partial class AbstractCamera : EntityComponent<Entities.Lucker>, ISingletonComponent
{
	public virtual bool ShouldShowCursor => false;
	public Vector3 CameraPosition { get; set; }
	protected Rotation CameraRotation { get; set; }
	protected float FieldOfView { get; set; }
	protected IEntity FirstPersonViewer { get; set; }
	protected Transform SoundSource { get; set; }
	
	/// <summary>
	/// Handles any input-independent camera updates (ie following a pawn)
	/// </summary>
	protected abstract void UpdateCameraParameters();

	/// <summary>
	/// Handles any input dependent camera updates (ie moving around a top down cam)
	/// </summary>
	public virtual void BuildInput() { }

	/// <summary>
	/// Applies Camera parameters to the static Camera
	/// </summary>
	public virtual void Update()
	{
		UpdateCameraParameters();
		Camera.Position = CameraPosition;
		Camera.Rotation = CameraRotation;
		Camera.FieldOfView = FieldOfView;
		Camera.FirstPersonViewer = FirstPersonViewer;
		Sound.Listener = SoundSource;
	}
}
