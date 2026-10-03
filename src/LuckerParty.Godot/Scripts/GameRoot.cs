using Godot;
using LuckerParty.Core;

namespace LuckerParty.Godot;

public partial class GameRoot : Node
{
    private CanvasLayer _menu = null!;
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

    public void EnterParty()
    {
        if (_party is not null) return;
        _connecting = false;
        _menu.Visible = false;
        _party = new PartyView { Name = "PartyUi", Root = this }; AddChild(_party);
    }
    public void EnterArena()
    {
        if (World is not null) return;
        _connecting = false;
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
        _menu.Visible = true;
        if (_connecting) ShowJoin(); else ShowHome();
        _connecting = false;
        SetMenuBusy(false);
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
