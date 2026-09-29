using Godot;
using LuckerParty.Core;

namespace LuckerParty.Godot;

public partial class GameRoot : Node
{
    private CanvasLayer _menu = null!;
    private LineEdit _name = null!, _address = null!;
    private SpinBox _port = null!;
    private Label _message = null!;
    private Button _host = null!, _join = null!, _practice = null!, _cancel = null!, _sandbox = null!;
    private PartyView? _party;
    private bool _automationMode;
    public MultiplayerSession Session { get; private set; } = null!;
    public TestBed? World { get; private set; }
    public bool PartyMenuOpen => _party?.MenuOpen == true;
    public bool PartyStandingsVisible => _party?.StandingsVisible == true;
    public string LastMessage { get; private set; } = "";

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--launcher-control")) AddChild(new LauncherControl { Name = "LauncherControl" });
        _automationMode = args.Contains("--control-file") || args.Contains("--probe-file") || args.Contains("--server");
        Session = new MultiplayerSession { Name = "Network", OwnerRoot = this };
        AddChild(Session);
        BuildMenu();
        if (args.Contains("--smoke-test") || args.Contains("--camera-check") || args.Contains("--capture")) Practice();
        if (args.Contains("--control-file") || args.Contains("--probe-file") || args.Contains("--exit-after"))
        {
            var automation = new NetworkAutomation { Name = "Automation", Root = this,
                ControlPath = Argument(args, "--control-file"), ProbePath = Argument(args, "--probe-file"),
                ExitAfter = double.TryParse(Argument(args, "--exit-after"), out var seconds) ? seconds : 0 };
            Session.Automation = automation; AddChild(automation);
        }
        if (args.Contains("--server") || args.Contains("--host"))
            if (!Session.Host(Argument(args, "--name") ?? "Host", CliPort(args), args.Contains("--server"), args.Contains("--party"))) GetTree().Quit(1);
        if (Argument(args, "--join") is { } address)
            Session.Join(Argument(args, "--name") ?? "Player", address, CliPort(args));
        if (DisplayServer.GetName() == "headless") Engine.MaxFps = args.Contains("--network-camera-check") ? 240 : 120;
        GD.Print($"MAIN_READY: version={GameBuild.Version}");
        if (Argument(args, "--menu-capture") is { } capture) CaptureMenu(capture);
        if (Argument(args, "--arena-capture") is { } arenaCapture) CaptureMenu(arenaCapture, true);
    }

    private static int CliPort(string[] args) => int.TryParse(Argument(args, "--port"), out var port) ? port : MultiplayerSession.DefaultPort;
    private static string? Argument(string[] args, string key)
    { var index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }

    private void BuildMenu()
    {
        GetWindow().Title = $"Lucker Party — {GameBuild.Version}";
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _menu = new CanvasLayer(); AddChild(_menu);
        var backdrop = new ColorRect { Color = new Color("fffdf8") };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); _menu.AddChild(backdrop);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); _menu.AddChild(scroll);
        var center = new CenterContainer { Theme = GD.Load<Theme>("res://UI/PartyTheme.tres"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; scroll.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(600, 0) }; center.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 12);
        panel.AddChild(margin);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 8); margin.AddChild(column);
        column.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://UI/Assets/Brand.svg"), CustomMinimumSize = new Vector2(0, 80),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        column.AddChild(new Label { Text = $"{GameBuild.Version}  /  Your next little party" });
        column.AddChild(new Label { Text = "Display name" });
        _name = new LineEdit { Text = "Player", MaxLength = PlayerNames.MaxLength, PlaceholderText = "Your name" }; column.AddChild(_name);
        column.AddChild(new Label { Text = "Server IP or hostname (for Join)" });
        _address = new LineEdit { Text = "127.0.0.1", PlaceholderText = "192.168.1.100", MaxLength = 253 }; column.AddChild(_address);
        var portRow = new HBoxContainer(); column.AddChild(portRow);
        portRow.AddChild(new Label { Text = "Port" });
        _port = new SpinBox { MinValue = 1024, MaxValue = 65535, Value = MultiplayerSession.DefaultPort, Step = 1 }; portRow.AddChild(_port);
        var buttons = new HBoxContainer(); buttons.AddThemeConstantOverride("separation", 12); column.AddChild(buttons);
        _host = new Button { Text = "Host party", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _join = new Button { Text = "Join", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        buttons.AddChild(_host); buttons.AddChild(_join);
        _host.Pressed += () => { SaveProfile(); Session.Host(_name.Text, (int)_port.Value, party: true); };
        _join.Pressed += () => { SaveProfile(); Session.Join(_name.Text, _address.Text, (int)_port.Value); };
        _address.TextSubmitted += _ => { if (!_join.Disabled) { SaveProfile(); Session.Join(_name.Text, _address.Text, (int)_port.Value); } };
        var secondary = new HBoxContainer(); column.AddChild(secondary);
        _practice = new Button { Text = "Practice offline", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; secondary.AddChild(_practice); _practice.Pressed += Practice;
        _sandbox = new Button { Text = "Multiplayer sandbox", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; secondary.AddChild(_sandbox);
        _sandbox.Pressed += () => { SaveProfile(); Session.Host(_name.Text, (int)_port.Value); };
        _cancel = new Button { Text = "Cancel connection", Visible = false }; column.AddChild(_cancel);
        _cancel.Pressed += () => Session.Leave("Connection cancelled.");
        column.AddChild(new Label { Text = "2–8 party players. Join 127.0.0.1 to test on this PC.\nLAN: host's local IP. Internet: forward this UDP port.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false }; column.AddChild(_message);
        var quit = new Button { Text = "Quit" }; column.AddChild(quit); quit.Pressed += () => GetTree().Quit();
        var profile = new ConfigFile();
        if (!_automationMode && profile.Load("user://profile.cfg") == Error.Ok)
        {
            _name.Text = PlayerNames.Clean(profile.GetValue("player", "name", "Player").AsString());
            _address.Text = profile.GetValue("connection", "address", "127.0.0.1").AsString();
            _port.Value = profile.GetValue("connection", "port", MultiplayerSession.DefaultPort).AsInt32();
        }
    }

    private void SaveProfile()
    {
        if (_automationMode) return;
        var profile = new ConfigFile();
        profile.SetValue("player", "name", PlayerNames.Clean(_name.Text));
        profile.SetValue("connection", "address", _address.Text.Trim());
        profile.SetValue("connection", "port", (int)_port.Value);
        var error = profile.Save("user://profile.cfg");
        if (error != Error.Ok) GD.PushWarning($"Could not save profile: {error}");
    }
    public void SaveName(string name) { _name.Text = name; SaveProfile(); }
    public void SetMessage(string message) { LastMessage = message; _message.Text = message; _message.Visible = message.Length > 0; }
    public void SetConnecting(string message)
    { SetMessage(message); _host.Disabled = _join.Disabled = _practice.Disabled = _sandbox.Disabled = true; _cancel.Visible = true; }
    public void EnterParty()
    {
        if (_party is not null) return;
        _menu.Visible = false;
        _party = new PartyView { Name = "PartyUi", Root = this }; AddChild(_party);
    }
    public void EnterArena()
    {
        if (World is not null) return;
        _menu.Visible = false;
        World = new TestBed { Name = "Arena", Session = Session }; AddChild(World);
    }
    private void Practice()
    {
        if (World is not null || Session.Active) return;
        _menu.Visible = false;
        World = new TestBed { Name = "Arena" }; AddChild(World);
    }
    public void ReturnToMenu(string message)
    {
        if (_party is not null) { RemoveChild(_party); _party.QueueFree(); _party = null; }
        if (World is not null)
        {
            World.SetProcess(false); World.SetPhysicsProcess(false);
            foreach (var player in World.GetChildren().OfType<FirstPersonPlayer>()) player.SetPhysicsProcess(false);
            World.QueueFree(); World = null;
        }
        Engine.PhysicsTicksPerSecond = 60;
        _menu.Visible = true; _host.Disabled = _join.Disabled = _practice.Disabled = _sandbox.Disabled = false; _cancel.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetWindow().Title = $"Lucker Party — {GameBuild.Version}";
        SetMessage(message);
    }
    private async void CaptureMenu(string path, bool arena = false)
    {
        if (arena)
        {
            var deadline = Time.GetTicksMsec() + 15000;
            while (Session.Players.Count < 2 || Session.Players.Values.All(p => p.Avatar?.LocalControl != true))
            {
                if (Time.GetTicksMsec() > deadline) { GetTree().Quit(1); return; }
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        }
        for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"MENU_CAPTURE: {error}"); GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public async void CaptureUi(string path)
    {
        _party?.ShowParty();
        for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"PARTY_CAPTURE: {path} {error}");
    }
    public void ClickPartyAction(string text) => _party?.ClickForScenario(text);
    public void PartyKeyForScenario(string key, bool pressed)
    {
        if (_party is not null && Enum.TryParse<Key>(key, out var code))
            GetViewport().PushInput(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = pressed }, true);
    }
}
