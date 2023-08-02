using Sandbox;
using System;
using System.Linq;

namespace LuckerGame.Components.Lucker.Cameras;

/// <summary>
/// A top down camera that can be 
/// </summary>
public partial class RTSCamera : AbstractCamera, ISingletonComponent
{
	private const float MaxDistance = 400f;
	private const float MinDistance = 200f;
	private const float Speed = 4f;
	protected float WheelSpeed => 5f;
	protected Vector2 DistanceClamp => new( MinDistance, MaxDistance );
	protected float CameraDistance = MinDistance;
	protected float TargetDistance = MinDistance;

	private Vector3 CameraPositionOffset = Vector3.Zero;

	protected override void UpdateCameraParameters()
	{
		CameraPosition = Entity.Position + CameraPositionOffset;
		CameraRotation = Rotation.FromPitch( -270 );
		FieldOfView = 70f;
		FirstPersonViewer = null;
		CameraPosition = CameraPosition.WithZ( CameraDistance );
		SoundSource = new Transform { Position = CameraPosition };
	}

	public override void BuildInput()
	{
		// Zooming in and out
		var wheel = Input.MouseWheel;
		if ( wheel != 0 )
		{
			TargetDistance -= wheel * WheelSpeed;
			TargetDistance = TargetDistance.Clamp( DistanceClamp.x, DistanceClamp.y );
		}

		CameraDistance = CameraDistance.LerpTo( TargetDistance, Time.Delta * 10f );
		
		// Moving
		CameraPositionOffset += Input.AnalogMove * Speed;
	}
}
