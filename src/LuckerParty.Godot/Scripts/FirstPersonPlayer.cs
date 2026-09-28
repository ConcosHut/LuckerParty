using Godot;
using LuckerParty.Core;

namespace LuckerParty.Godot;

public partial class FirstPersonPlayer : CharacterBody3D
{
    private const float MouseSensitivity = 0.0025f;
    private readonly MovementSettings _settings = new();
    private Node3D _head = null!;
    private Camera3D _camera = null!;
    private float _pitch;
    private float _yaw;

    public Vector3 SpawnPosition { get; set; } = new(0, 0.05f, 6);
    public bool ControlsEnabled { get; set; } = true;
    public bool InterpolateCameraPosition { get; set; } = true;
    public Vector3 CameraPosition => _camera.GlobalPosition;
    public Vector3 CameraRotation => _camera.GlobalRotation;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _camera = GetNode<Camera3D>("Head/Camera");
        // Create Godot's interpolation history before the initial teleport.
        GetGlobalTransformInterpolated();
        ResetToSpawn();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (ControlsEnabled && Input.MouseMode == Input.MouseModeEnum.Captured
            && input is InputEventMouseMotion mouse)
        {
            ApplyMouseLook(mouse.ScreenRelative);
        }
    }

    public void ApplyMouseLook(Vector2 screenRelative)
    {
        // ScreenRelative keeps sensitivity independent of window stretching.
        _yaw = Mathf.Wrap(_yaw - screenRelative.X * MouseSensitivity, -Mathf.Pi, Mathf.Pi);
        _pitch = Mathf.Clamp(_pitch - screenRelative.Y * MouseSensitivity,
            Mathf.DegToRad(-85), Mathf.DegToRad(85));
        _head.Rotation = new Vector3(_pitch, 0, 0);
        UpdateCamera();
    }

    public override void _Process(double delta)
    {
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        // Smooth translation at render frequency; mouse rotation stays immediate.
        // The top-level camera cannot inherit a second, unsmoothed parent motion.
        var bodyPosition = InterpolateCameraPosition
            ? GetGlobalTransformInterpolated().Origin
            : GlobalPosition;
        _camera.GlobalPosition = bodyPosition + _head.Position;
        _camera.GlobalRotation = new Vector3(_pitch, _yaw, 0);
    }

    public override void _PhysicsProcess(double delta)
    {
        // Interpolated physics transforms must only change on physics ticks.
        Rotation = new Vector3(0, _yaw, 0);
        var velocity = Velocity;
        if (!IsOnFloor()) velocity.Y -= _settings.Gravity * (float)delta;

        var input = ControlsEnabled
            ? Input.GetVector("move_left", "move_right", "move_forward", "move_back")
            : Vector2.Zero;
        var direction = GlobalBasis * new Vector3(input.X, 0, input.Y);
        var speed = Input.IsActionPressed("sprint") ? _settings.SprintSpeed : _settings.WalkSpeed;
        velocity.X = direction.X * speed;
        velocity.Z = direction.Z * speed;

        if (ControlsEnabled && IsOnFloor() && Input.IsActionJustPressed("jump"))
            velocity.Y = _settings.JumpSpeed;

        Velocity = velocity;
        MoveAndSlide();
        if (Position.Y < -10) ResetToSpawn();
    }

    public void ResetToSpawn()
    {
        Position = SpawnPosition;
        Velocity = Vector3.Zero;
        Rotation = Vector3.Zero;
        _pitch = 0;
        _yaw = 0;
        if (_head is not null) _head.Rotation = Vector3.Zero;
        ResetPhysicsInterpolation();
        if (_camera is not null) UpdateCamera();
    }
}
