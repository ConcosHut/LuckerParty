using Godot;
using LuckerParty.Core;

namespace LuckerParty.Godot;

public readonly record struct PlayerState(Vector3 Position, Vector3 Velocity, float Yaw, float Pitch, bool Grounded, int Ack);

public partial class FirstPersonPlayer : CharacterBody3D
{
    private const float MouseSensitivity = 0.0025f;
    private readonly MovementSettings _settings = new();
    private Node3D _head = null!;
    private Camera3D _camera = null!;
    private Label3D _nameLabel = null!;
    private float _pitch, _yaw;
    private bool _grounded, _resetRequested;
    private int _sequence;
    private readonly List<NetworkInput> _pending = new();
    private Vector3 _visualCorrection, _previousPresentation, _currentPresentation;
    private PlayerState _remoteTarget;
    private bool _hasRemoteTarget;

    public Vector3 SpawnPosition { get; set; } = new(0, 0.05f, 6);
    public bool ControlsEnabled { get; set; } = true;
    public bool InterpolateCameraPosition { get; set; } = true;
    public MultiplayerSession? Session { get; set; }
    public int PeerId { get; set; } = 1;
    public bool LocalControl { get; set; } = true;
    public int LastInput { get; private set; }
    public int LastServerAck { get; private set; }
    public float LargestCorrection { get; private set; }
    public int PendingInputs => _pending.Count;
    public Vector3 CameraPosition => _camera.GlobalPosition;
    public Vector3 CameraRotation => _camera.GlobalRotation;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _camera = GetNode<Camera3D>("Head/Camera");
        _camera.Current = LocalControl;
        if (Session is not null) AddAppearance();
        if (Session is not null && !Session.IsServer && !LocalControl)
        {
            CollisionLayer = 0; CollisionMask = 0;
        }
        GetGlobalTransformInterpolated();
        ResetToSpawn();
    }

    private void AddAppearance()
    {
        // Player-player blocking is intentionally absent; the server still owns arena collisions.
        CollisionLayer = 2; CollisionMask = 1;
        var color = Color.FromHsv((PeerId % 17) / 17f, .65f, .85f);
        AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, .9f, 0), Visible = !LocalControl,
            Mesh = new CapsuleMesh { Radius = .35f, Height = 1.8f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = .8f }
        });
        _nameLabel = new Label3D
        {
            Position = new Vector3(0, 2.15f, 0), Visible = !LocalControl,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 32, OutlineSize = 6, PixelSize = .0025f, FixedSize = true,
            Modulate = Colors.White
        };
        AddChild(_nameLabel);
    }

    public void SetDisplayName(string name) { if (_nameLabel is not null) _nameLabel.Text = name; }

    public override void _UnhandledInput(InputEvent input)
    {
        if (LocalControl && ControlsEnabled && Input.MouseMode == Input.MouseModeEnum.Captured
            && input is InputEventMouseMotion mouse) ApplyMouseLook(mouse.ScreenRelative);
    }

    public void ApplyMouseLook(Vector2 screenRelative)
    {
        _yaw = Mathf.Wrap(_yaw - screenRelative.X * MouseSensitivity, -Mathf.Pi, Mathf.Pi);
        _pitch = Mathf.Clamp(_pitch - screenRelative.Y * MouseSensitivity, Mathf.DegToRad(-85), Mathf.DegToRad(85));
        _head.Rotation = new Vector3(_pitch, 0, 0);
        UpdateCamera();
    }

    public override void _Process(double delta)
    {
        if (!LocalControl) return;
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (!LocalControl) return;
        var bodyPosition = !InterpolateCameraPosition ? GlobalPosition
            : Session is null ? GetGlobalTransformInterpolated().Origin
            : _previousPresentation.Lerp(_currentPresentation, (float)Engine.GetPhysicsInterpolationFraction());
        _camera.GlobalPosition = bodyPosition + _head.Position;
        _camera.GlobalRotation = new Vector3(_pitch, _yaw, 0);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Session is not null && !Session.IsServer && !LocalControl)
        {
            if (_hasRemoteTarget)
            {
                Position = Position.Lerp(_remoteTarget.Position, 1 - Mathf.Exp(-18 * (float)delta));
                Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, _remoteTarget.Yaw, .3f), 0);
            }
            return;
        }
        if (LocalControl && Session is not null)
        {
            _previousPresentation = _currentPresentation;
            _visualCorrection *= Mathf.Exp(-18 * (float)delta);
        }
        NetworkInput command;
        if (LocalControl)
        {
            var input = ControlsEnabled ? Input.GetVector("move_left", "move_right", "move_forward", "move_back") : Vector2.Zero;
            command = new(++_sequence, input.X, input.Y, _yaw, _pitch,
                ControlsEnabled && Input.IsActionPressed("sprint"),
                ControlsEnabled && Input.IsActionJustPressed("jump"), _resetRequested);
            _resetRequested = false;
            if (Session?.Automation is { } automation) command = automation.Input(command);
            if (Session is not null && !Session.IsServer)
            {
                _pending.Add(command);
                if (_pending.Count > 120) _pending.RemoveAt(0);
                Session.SendInputs(InputCodec.Encode(_pending));
            }
        }
        else
        {
            var commands = Session!.ConsumeInputs(PeerId);
            if (commands.Length == 0) Simulate(new(LastInput, 0, 0, _yaw, _pitch, false, false, false), (float)delta);
            foreach (var input in commands) { Simulate(input, 1f / 60); LastInput = input.Sequence; }
            return;
        }
        Simulate(command, (float)delta);
        LastInput = command.Sequence;
        if (LocalControl && Session is not null) _currentPresentation = Position + _visualCorrection;
    }

    private void Simulate(NetworkInput command, float delta)
    {
        if (command.Reset) ResetToSpawn();
        _yaw = command.Reset ? 0 : command.Yaw;
        _pitch = command.Reset ? 0 : command.Pitch;
        Rotation = new Vector3(0, _yaw, 0);
        _head.Rotation = new Vector3(_pitch, 0, 0);
        var velocity = Velocity;
        if (!_grounded) velocity.Y -= _settings.Gravity * delta;
        var input = new Vector2(command.X, command.Y).LimitLength();
        var direction = GlobalBasis * new Vector3(input.X, 0, input.Y);
        var speed = command.Sprint ? _settings.SprintSpeed : _settings.WalkSpeed;
        velocity.X = direction.X * speed; velocity.Z = direction.Z * speed;
        if (_grounded && command.Jump) velocity.Y = _settings.JumpSpeed;
        Velocity = velocity;
        MoveAndSlide();
        _grounded = IsOnFloor();
        if (Position.Y < -10) ResetToSpawn();
    }

    public PlayerState State => new(Position, Velocity, _yaw, _pitch, _grounded, LastInput);

    public void ReceiveState(PlayerState state)
    {
        if (!LocalControl)
        {
            if (!_hasRemoteTarget || Position.DistanceTo(state.Position) > 4)
            {
                Position = state.Position; ResetPhysicsInterpolation();
            }
            _remoteTarget = state; _hasRemoteTarget = true;
            return;
        }
        LastServerAck = state.Ack;
        _pending.RemoveAll(c => c.Sequence <= state.Ack);
        var before = Position;
        var lookYaw = _yaw; var lookPitch = _pitch;
        Position = state.Position; Velocity = state.Velocity; _grounded = state.Grounded;
        foreach (var command in _pending) Simulate(command, 1f / 60);
        _yaw = lookYaw; _pitch = lookPitch;
        _head.Rotation = new Vector3(_pitch, 0, 0);
        var distance = before.DistanceTo(Position);
        LargestCorrection = Math.Max(LargestCorrection, distance);
        if (distance > .25f) GD.Print($"NET_CORRECTION: peer={PeerId} distance={distance:F3} ack={state.Ack} pending={_pending.Count}");
        if (distance < 2) _visualCorrection += before - Position;
        else
        {
            _visualCorrection = Vector3.Zero;
            _previousPresentation = _currentPresentation = Position;
            ResetPhysicsInterpolation();
        }
        UpdateCamera();
    }

    public void RequestReset() { _resetRequested = true; _yaw = 0; _pitch = 0; }

    public void ResetToSpawn()
    {
        Position = SpawnPosition; Velocity = Vector3.Zero; Rotation = Vector3.Zero;
        _pitch = 0; _yaw = 0; _grounded = false; _visualCorrection = Vector3.Zero;
        _previousPresentation = _currentPresentation = Position;
        if (_head is not null) _head.Rotation = Vector3.Zero;
        ResetPhysicsInterpolation();
        if (_camera is not null) UpdateCamera();
    }
}
