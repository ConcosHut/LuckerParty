using Sandbox;

namespace LuckerGame.Components.Lucker.Cameras;

public partial class FixedCamera : AbstractCamera, ISingletonComponent
{
	[Net] public float FieldOfViewValue { get; set; } = 70f;
	public override bool ShouldShowCursor => true;
	protected override void UpdateCameraParameters()
	{
		FieldOfView = FieldOfViewValue;
		FirstPersonViewer = null;
		SoundSource = new Transform { Position = CameraPosition };
	}

	/// <summary>
	/// ClientRpc doesn't allow for nullable versions of non-nullable types
	/// so here we are
	/// </summary>
	public void UpdateCameraPositionRotation( Vector3? position = null, Rotation? rotation = null )
	{
		CameraPosition = (Vector3)(position.Equals( null ) ? CameraPosition : position);
		CameraRotation = (Rotation)(rotation.Equals( null ) ? CameraRotation : rotation);
	}

	[ClientRpc]
	public void LookAt( Vector3 position )
	{
		UpdateCameraPositionRotation( position );
	}
	[ClientRpc]
	public void LookAt( Rotation rotation )
	{
		UpdateCameraPositionRotation( rotation: rotation );
	}
	[ClientRpc]
	public void LookAt( Vector3 position, Rotation rotation )
	{
		UpdateCameraPositionRotation( position, rotation );
	}
}
