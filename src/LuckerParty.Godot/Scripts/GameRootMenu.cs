using Godot;
using LuckerParty.Core;

namespace LuckerParty.Godot;

public partial class GameRoot
{
    private LineEdit _name = null!, _address = null!;
    private SpinBox _port = null!, _hostPort = null!;
    private Label _message = null!, _joinMessage = null!;
    private Button _host = null!, _join = null!, _practice = null!, _cancel = null!, _sandbox = null!, _back = null!;
    private Control _homePage = null!, _joinPage = null!;
    private bool _connecting;

    private void BuildMenu()
    {
        GetWindow().Title = $"Lucker Party — {GameBuild.Version}";
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _menu = new CanvasLayer(); AddChild(_menu);
        var background = new ColorRect { Color = PartyUiStyle.Cream };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _menu.AddChild(background);
        var lowerSand = new ColorRect { Color = new Color("f5f0e7"), MouseFilter = Control.MouseFilterEnum.Ignore };
        lowerSand.AnchorLeft = 0; lowerSand.AnchorRight = 1;
        lowerSand.AnchorTop = .79f; lowerSand.AnchorBottom = 1;
        _menu.AddChild(lowerSand);

        _homePage = Page(); _menu.AddChild(_homePage);
        var home = PageColumn(_homePage);
        var top = new HBoxContainer(); home.AddChild(top);
        top.AddChild(PartyUiStyle.Label($"VERSION {GameBuild.Version}", 14, muted: true));
        top.AddSpacer(false);
        top.AddChild(PartyUiStyle.Label("DISPLAY NAME", 14, muted: true));
        _name = new LineEdit { Text = "Player", MaxLength = PlayerNames.MaxLength,
            PlaceholderText = "Your name", CustomMinimumSize = new Vector2(180, 46) };
        top.AddChild(_name);
        _name.TextSubmitted += _ => SaveProfile();
        _name.FocusExited += () => SaveProfile();

        home.AddSpacer(false);
        home.AddChild(Logo(150));
        var tagline = PartyUiStyle.Label("YOUR NEXT LITTLE PARTY", 18, muted: true);
        tagline.HorizontalAlignment = HorizontalAlignment.Center; home.AddChild(tagline);
        home.AddSpacer(false);
        var choices = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        choices.AddThemeConstantOverride("separation", 22); home.AddChild(choices);
        _host = ModeCard("Host Party", "Create a room", false);
        choices.AddChild(_host);
        _host.Pressed += () => { SaveProfile(); Session.Host(_name.Text, (int)_hostPort.Value, party: true); };
        var joinCard = ModeCard("Join Party", "Enter an address", true);
        choices.AddChild(joinCard); joinCard.Pressed += ShowJoin;
        var options = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        home.AddChild(options);
        var networkToggle = new Button { Text = $"Host network settings  ·  UDP {MultiplayerSession.DefaultPort}", Flat = true };
        PartyUiStyle.Button(networkToggle, true); options.AddChild(networkToggle);
        var hostNetwork = new HBoxContainer { Visible = false };
        options.AddChild(hostNetwork);
        hostNetwork.AddChild(PartyUiStyle.Label("Host UDP port", 16));
        _hostPort = PortField(); hostNetwork.AddChild(_hostPort);
        _hostPort.ValueChanged += value => networkToggle.Text = $"Host network settings  ·  UDP {(int)value}";
        networkToggle.Pressed += () => hostNetwork.Visible = !hostNetwork.Visible;
        home.AddSpacer(false);
        var secondary = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        secondary.AddThemeConstantOverride("separation", 16); home.AddChild(secondary);
        _practice = SmallAction(secondary, "Practice offline"); _practice.Pressed += Practice;
        _sandbox = SmallAction(secondary, "Host multiplayer sandbox");
        _sandbox.Pressed += () => { SaveProfile(); Session.Host(_name.Text, (int)_hostPort.Value); };
        var homeBottom = new HBoxContainer(); home.AddChild(homeBottom);
        _message = PartyUiStyle.Label("", 16, muted: true); _message.Visible = false;
        _message.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; homeBottom.AddChild(_message);
        var quit = SmallAction(homeBottom, "Quit"); quit.Pressed += () => GetTree().Quit();

        _joinPage = Page(); _menu.AddChild(_joinPage);
        var join = PageColumn(_joinPage);
        _back = SmallAction(join, "←  Back");
        _back.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        _back.Pressed += ShowHome;
        join.AddSpacer(false);
        join.AddChild(Logo(140));
        var joinPanel = new PanelContainer { CustomMinimumSize = new Vector2(620, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        joinPanel.AddThemeStyleboxOverride("panel", PartyUiStyle.Box(PartyUiStyle.Sand, 22, padding: 30));
        join.AddChild(joinPanel);
        var form = new VBoxContainer(); form.AddThemeConstantOverride("separation", 18); joinPanel.AddChild(form);
        var title = PartyUiStyle.Label("Join a party", 42, heading: true);
        title.HorizontalAlignment = HorizontalAlignment.Center; form.AddChild(title);
        form.AddChild(PartyUiStyle.Label("Server address", 19));
        _address = new LineEdit { Text = "127.0.0.1", PlaceholderText = "192.168.1.100 or hostname",
            MaxLength = 253, CustomMinimumSize = new Vector2(0, 52) }; form.AddChild(_address);
        form.AddChild(PartyUiStyle.Label("Enter the host's IP or hostname. Same PC? Use 127.0.0.1.", 15, muted: true));
        var advanced = SmallAction(form, $"Advanced  ·  UDP port {MultiplayerSession.DefaultPort}  ⌄");
        var portRow = new HBoxContainer { Visible = false }; form.AddChild(portRow);
        portRow.AddChild(PartyUiStyle.Label("UDP port", 17));
        _port = PortField(); portRow.AddChild(_port);
        advanced.Pressed += () => portRow.Visible = !portRow.Visible;
        _port.ValueChanged += value => advanced.Text = $"Advanced  ·  UDP port {(int)value}  ⌄";
        _join = new Button { Text = "Connect", CustomMinimumSize = new Vector2(0, 62) };
        PartyUiStyle.Button(_join); form.AddChild(_join);
        _join.Pressed += Connect;
        _address.TextSubmitted += _ => { if (!_join.Disabled) Connect(); };
        _cancel = SmallAction(form, "Cancel connection"); _cancel.Visible = false;
        _cancel.Pressed += () => Session.Leave("Connection cancelled.");
        _joinMessage = PartyUiStyle.Label("", 16, muted: true); _joinMessage.Visible = false;
        form.AddChild(_joinMessage);
        join.AddSpacer(false);

        var profile = new ConfigFile();
        if (!_automationMode && profile.Load("user://profile.cfg") == Error.Ok)
        {
            _name.Text = PlayerNames.Clean(profile.GetValue("player", "name", "Player").AsString());
            _address.Text = profile.GetValue("connection", "address", "127.0.0.1").AsString();
            var previousPort = profile.GetValue("connection", "port", MultiplayerSession.DefaultPort);
            _port.Value = profile.GetValue("connection", "join_port", previousPort).AsInt32();
            _hostPort.Value = profile.GetValue("connection", "host_port", previousPort).AsInt32();
        }
        ShowHome();
    }

    private static Control Page()
    {
        var page = new MarginContainer();
        page.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right" }) page.AddThemeConstantOverride("margin_" + side, 44);
        foreach (var side in new[] { "top", "bottom" }) page.AddThemeConstantOverride("margin_" + side, 24);
        page.Theme = GD.Load<Theme>("res://UI/PartyTheme.tres");
        return page;
    }

    private static VBoxContainer PageColumn(Control parent)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        parent.AddChild(column);
        return column;
    }

    private static TextureRect Logo(float height) => new()
    {
        Texture = GD.Load<Texture2D>("res://UI/Assets/Brand.svg"),
        CustomMinimumSize = new Vector2(0, height),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
    };

    private static Button ModeCard(string title, string detail, bool secondary)
    {
        var button = new Button { Text = $"{title}\n\n{detail}", CustomMinimumSize = new Vector2(315, 160),
            ClipText = true };
        PartyUiStyle.Button(button, secondary);
        button.AddThemeFontSizeOverride("font_size", 26);
        return button;
    }

    private static Button SmallAction(Node parent, string title)
    {
        var button = new Button { Text = title };
        PartyUiStyle.Button(button, true); parent.AddChild(button);
        return button;
    }

    private static SpinBox PortField() => new()
    {
        MinValue = 1024, MaxValue = 65535, Value = MultiplayerSession.DefaultPort,
        Step = 1, CustomMinimumSize = new Vector2(130, 42)
    };

    private void Connect()
    {
        var address = _address.Text.Trim();
        // The lobby's Copy action includes the port. Accept that paste without
        // requiring the guest to find the Advanced control.
        var separator = address.LastIndexOf(':');
        if (separator > 0 && address.IndexOf(':') == separator)
        {
            if (!int.TryParse(address[(separator + 1)..], out var invitePort) || invitePort is < 1024 or > 65535)
            { SetMessage("Enter an address such as 192.168.1.100:27015."); return; }
            _address.Text = address[..separator];
            _port.Value = invitePort;
        }
        SaveProfile();
        Session.Join(_name.Text, _address.Text, (int)_port.Value);
    }

    private void ShowHome() { _joinPage.Visible = false; _homePage.Visible = true; }
    private void ShowJoin() { _homePage.Visible = false; _joinPage.Visible = true; }
    public string MenuPage => _joinPage.Visible ? "join" : "home";
    public void ShowHomeForScenario() => ShowHome();
    public void ShowJoinForScenario() => ShowJoin();

    private void SaveProfile()
    {
        if (_automationMode) return;
        var profile = new ConfigFile();
        profile.SetValue("player", "name", PlayerNames.Clean(_name.Text));
        profile.SetValue("connection", "address", _address.Text.Trim());
        profile.SetValue("connection", "port", (int)_port.Value);
        profile.SetValue("connection", "join_port", (int)_port.Value);
        profile.SetValue("connection", "host_port", (int)_hostPort.Value);
        var error = profile.Save("user://profile.cfg");
        if (error != Error.Ok) GD.PushWarning($"Could not save profile: {error}");
    }

    public void SaveName(string name) { _name.Text = PlayerNames.Clean(name); SaveProfile(); }
    public void SetMessage(string message)
    {
        LastMessage = message;
        foreach (var label in new[] { _message, _joinMessage })
        { label.Text = message; label.Visible = message.Length > 0; }
    }
    public void SetConnecting(string message)
    { _connecting = true; ShowJoin(); SetMessage(message); SetMenuBusy(true); }
    private void SetMenuBusy(bool busy)
    {
        _host.Disabled = _join.Disabled = _practice.Disabled = _sandbox.Disabled = busy;
        _back.Disabled = busy;
        _cancel.Visible = busy;
    }
}
