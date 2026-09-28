using Godot;
using System.Text.Json;
using GameEnvironment = Godot.Environment;

namespace LuckerParty.Godot;

public partial class TestBed : Node3D
{
    private FirstPersonPlayer _player = null!;
    private Control _pauseMenu = null!;
    private Label _status = null!;
    private Label _diagnostics = null!;
    private readonly Queue<double> _frameTimes = new();
    private double _diagnosticTimer;
    private bool _paused;

    public override void _Ready()
    {
        RegisterInputs();
        BuildWorld();
        _player = GD.Load<PackedScene>("res://Scenes/FirstPersonPlayer.tscn")
            .Instantiate<FirstPersonPlayer>();
        AddChild(_player);
        BuildInterface();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        GD.Print("PROTOTYPE_READY: first-person test bed loaded");

        var arguments = OS.GetCmdlineUserArgs();
        if (arguments.Contains("--smoke-test"))
            AddChild(new SmokeTest { Player = _player });
        if (arguments.Contains("--camera-check"))
            AddChild(new CameraInterpolationCheck { Player = _player });
        var captureIndex = Array.IndexOf(arguments, "--capture");
        if (captureIndex >= 0 && captureIndex + 1 < arguments.Length)
            CaptureFrame(arguments[captureIndex + 1]);
    }

    private static void RegisterInputs()
    {
        Bind("move_forward", Key.W, Key.Up);
        Bind("move_back", Key.S, Key.Down);
        Bind("move_left", Key.A, Key.Left);
        Bind("move_right", Key.D, Key.Right);
        Bind("jump", Key.Space);
        Bind("sprint", Key.Shift);
        Bind("reset", Key.R);
        Bind("pause", Key.Escape);
        Bind("diagnostics", Key.F3);
        Bind("interpolation", Key.F4);
        Bind("slow_physics", Key.F5);
    }

    private static void Bind(string action, params Key[] keys)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var key in keys)
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    private void BuildWorld()
    {
        AddChild(new WorldEnvironment
        {
            Environment = new GameEnvironment
            {
                BackgroundMode = GameEnvironment.BGMode.Color,
                BackgroundColor = new Color("9ebbd3"),
                AmbientLightSource = GameEnvironment.AmbientSource.Color,
                AmbientLightColor = Colors.White,
                AmbientLightEnergy = 0.25f,
                TonemapMode = GameEnvironment.ToneMapper.Linear
            }
        });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55, -30, 0),
            LightEnergy = 0.75f,
            ShadowEnabled = true
        });

        Box("Floor", new Vector3(0, -0.25f, 0), new Vector3(32, 0.5f, 32), new Color("c0c8cf"));
        var edgeColor = new Color("526779");
        Box("NorthWall", new Vector3(0, 0.6f, -16), new Vector3(32, 1.2f, 0.4f), edgeColor);
        Box("SouthWall", new Vector3(0, 0.6f, 16), new Vector3(32, 1.2f, 0.4f), edgeColor);
        Box("WestWall", new Vector3(-16, 0.6f, 0), new Vector3(0.4f, 1.2f, 32), edgeColor);
        Box("EastWall", new Vector3(16, 0.6f, 0), new Vector3(0.4f, 1.2f, 32), edgeColor);

        Box("RedBox", new Vector3(-4, 1, -4), new Vector3(2, 2, 2), new Color("ee5363"));
        Box("BlueBox", new Vector3(0, 1, -5), new Vector3(2, 2, 2), new Color("367ee8"));
        Box("YellowBox", new Vector3(4, 1, -4), new Vector3(2, 2, 2), new Color("f3bd45"));
        Box("GreenStep", new Vector3(4, 0.35f, 2), new Vector3(2, 0.7f, 2), new Color("36a982"));

        // A two-meter floor grid makes speed and depth easy to judge.
        for (var offset = -14; offset <= 14; offset += 2)
        {
            GridLine(new Vector3(offset, 0.004f, 0), new Vector3(0.025f, 0.005f, 31.5f));
            GridLine(new Vector3(0, 0.004f, offset), new Vector3(31.5f, 0.005f, 0.025f));
        }
    }

    private void Box(string name, Vector3 position, Vector3 size, Color color)
    {
        var body = new StaticBody3D { Name = name, Position = position };
        body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.85f }
        });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
    }

    private void GridLine(Vector3 position, Vector3 size)
    {
        AddChild(new MeshInstance3D
        {
            Position = position,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color("8f9da8"),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    private void BuildInterface()
    {
        var version = ReadBuildVersion();
        GetWindow().Title = $"Lucker Party — {version}";
        GD.Print($"GAME_BUILD: version={version}");
        var layer = new CanvasLayer();
        AddChild(layer);
        var root = new Control();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        layer.AddChild(root);
        root.AddChild(Text($"LUCKER PARTY  /  {version}", new Vector2(28, 24), 24));
        root.AddChild(Text("PROTOTYPE 01.1  /  FIRST-PERSON MOVEMENT TEST", new Vector2(28, 58), 14));
        var instructions = Text("WASD / arrows  Move     Mouse  Look     Shift  Sprint\nSpace  Jump     R  Reset     Esc  Menu     F3  Diagnostics", Vector2.Zero, 17);
        instructions.AnchorTop = instructions.AnchorBottom = 1;
        instructions.Position = new Vector2(28, -82);
        root.AddChild(instructions);
        _status = Text("", new Vector2(28, 84), 14);
        root.AddChild(_status);
        _diagnostics = Text("", new Vector2(28, 111), 14);
        _diagnostics.Visible = false;
        root.AddChild(_diagnostics);
        var crosshair = Text("+", Vector2.Zero, 24);
        crosshair.AnchorLeft = crosshair.AnchorRight = 0.5f;
        crosshair.AnchorTop = crosshair.AnchorBottom = 0.5f;
        crosshair.Position = new Vector2(-7, -18);
        root.AddChild(crosshair);

        _pauseMenu = new PanelContainer { Visible = false };
        _pauseMenu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _pauseMenu.Position = new Vector2(-180, -110);
        _pauseMenu.CustomMinimumSize = new Vector2(360, 220);
        root.AddChild(_pauseMenu);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        _pauseMenu.AddChild(column);
        column.AddChild(new Label { Text = "PAUSED", HorizontalAlignment = HorizontalAlignment.Center });
        var resume = new Button { Text = "Resume" };
        resume.Pressed += () => SetPaused(false);
        column.AddChild(resume);
        var reset = new Button { Text = "Reset position" };
        reset.Pressed += () => { _player.ResetToSpawn(); SetPaused(false); };
        column.AddChild(reset);
        var quit = new Button { Text = "Quit" };
        quit.Pressed += () => GetTree().Quit();
        column.AddChild(quit);
    }

    private static string ReadBuildVersion()
    {
        if (OS.HasFeature("editor")) return "DEVELOPMENT";
        var path = Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!, "build-info.json");
        if (!File.Exists(path)) return "UNVERSIONED BUILD";
        using var metadata = JsonDocument.Parse(File.ReadAllText(path));
        return metadata.RootElement.GetProperty("version").GetString()!;
    }

    private static Label Text(string text, Vector2 position, int size)
    {
        var label = new Label { Text = text, Position = position, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("172937"));
        return label;
    }

    public override void _Process(double delta)
    {
        _frameTimes.Enqueue(delta * 1000);
        if (_frameTimes.Count > 240) _frameTimes.Dequeue();
        _diagnosticTimer += delta;
        if (_diagnosticTimer < 0.25) return;
        _diagnosticTimer = 0;
        var sorted = _frameTimes.Order().ToArray();
        _status.Text = $"{Engine.GetFramesPerSecond()} FPS  /  {Engine.PhysicsTicksPerSecond} physics Hz  /  {(_player.IsOnFloor() ? "GROUNDED" : "AIRBORNE")}";
        _diagnostics.Text = $"F4  Camera interpolation: {(_player.InterpolateCameraPosition ? "ON" : "OFF (original)")}\n"
            + $"Frame time: avg {sorted.Average():F2} ms / p95 {sorted[(int)((sorted.Length - 1) * 0.95)]:F2} ms / max {sorted[^1]:F2} ms\n"
            + $"F5  Physics: {Engine.PhysicsTicksPerSecond} Hz (60 normal / 10 diagnostic)\n"
            + "Compare A/D with the mouse still. R resets position and view.";
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsActionPressed("pause"))
        {
            SetPaused(!_paused);
            GetViewport().SetInputAsHandled();
        }
        else if (!_paused && input.IsActionPressed("reset")) _player.ResetToSpawn();
        else if (input.IsActionPressed("diagnostics")) _diagnostics.Visible = !_diagnostics.Visible;
        else if (input.IsActionPressed("interpolation"))
        {
            _player.InterpolateCameraPosition = !_player.InterpolateCameraPosition;
            _diagnostics.Visible = true;
            GD.Print($"CAMERA_INTERPOLATION: {_player.InterpolateCameraPosition}");
        }
        else if (input.IsActionPressed("slow_physics"))
        {
            Engine.PhysicsTicksPerSecond = Engine.PhysicsTicksPerSecond == 60 ? 10 : 60;
            _player.ResetToSpawn();
            _diagnostics.Visible = true;
            GD.Print($"PHYSICS_TICK_RATE: {Engine.PhysicsTicksPerSecond}");
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut && _player is not null) SetPaused(true);
    }

    private void SetPaused(bool paused)
    {
        _paused = paused;
        _pauseMenu.Visible = paused;
        _player.ControlsEnabled = !paused;
        _player.SetPhysicsProcess(!paused);
        Input.MouseMode = paused ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }

    private async void CaptureFrame(string path)
    {
        for (var frame = 0; frame < 30; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"CAPTURE_RESULT: {error} {path}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }
}
