using Godot;
using LuckerParty.Core;

namespace LuckerParty.Godot;

/// <summary>Party presentation and input intent. The server owns all state and results.</summary>
public partial class PartyView : CanvasLayer
{
    public GameRoot Root { get; set; } = null!;
    private Control _shell = null!;
    private VBoxContainer _roster = null!, _stage = null!;
    private HBoxContainer _actions = null!;
    private Label _title = null!, _subtitle = null!, _timer = null!, _status = null!;
    private Control? _menuOverlay;
    private PartySnapshot? _state;
    private long _drawnRevision = -1;
    private double _receivedAt;
    private bool _menu, _standings, _needsRedraw;
    public bool MenuOpen => _menu;
    public bool StandingsVisible => _standings && !_menu;

    public override void _Ready()
    {
        _shell = GD.Load<PackedScene>("res://UI/PartyShell.tscn").Instantiate<Control>(); AddChild(_shell);
        _roster = _shell.GetNode<VBoxContainer>("Margin/Column/Body/RosterPanel/Scroll/Roster");
        _stage = _shell.GetNode<VBoxContainer>("Margin/Column/Body/StagePanel/Scroll/Stage");
        _shell.GetNode<PanelContainer>("Margin/Column/Body/StagePanel")
            .AddThemeStyleboxOverride("panel", PartyUiStyle.Box(new Color(1, 1, 1, 0), padding: 0));
        _actions = _shell.GetNode<HBoxContainer>("Margin/Column/Footer/Actions");
        _title = _shell.GetNode<Label>("Margin/Column/Header/Titles/Title");
        _subtitle = _shell.GetNode<Label>("Margin/Column/Header/Titles/Subtitle");
        _timer = _shell.GetNode<Label>("Margin/Column/Header/Timer");
        _status = _shell.GetNode<Label>("Margin/Column/Footer/Status");
        Root.Session.PartyChanged += Accept;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (Root.Session.PartyState is { } state) Accept(state);
    }

    public override void _ExitTree() => Root.Session.PartyChanged -= Accept;

    public void ShowParty()
    { _menu = _standings = false; RemoveMenuOverlay(); DrawState(); }

    // Only the explicit machine-local scenario driver calls this. Exercise the
    // real viewport pointer route instead of invoking button callbacks directly.
    public async void ClickForScenario(string text)
    {
        ShowParty();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var button = Buttons(_shell).FirstOrDefault(b => b.Text == text && !b.Disabled);
        if (button is null) { Root.SetMessage($"Scenario button unavailable: {text}"); return; }
        var point = button.GetGlobalRect().GetCenter();
        var viewport = GetViewport();
        viewport.NotifyMouseEntered();
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true }, true);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);

    }

    private static IEnumerable<Button> Buttons(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Button b) yield return b;
            foreach (var descendant in Buttons(child)) yield return descendant;
        }
    }

    private void Accept(PartySnapshot state)
    {
        _state = state; _receivedAt = Time.GetTicksMsec() / 1000d;
        if (_drawnRevision == state.Revision) return;
        _needsRedraw = true;
    }

    public override void _Process(double delta)
    {
        if (_state is not { } state) return;
        // A roster update during a press must not destroy the button before
        // release. Coalesce network changes and refresh after the gesture ends.
        if (_needsRedraw && !Buttons(_shell).Any(button => button.IsPressed())) DrawState();
        var remaining = Math.Max(0, state.Deadline - state.ServerTime - (Time.GetTicksMsec() / 1000d - _receivedAt));
        _timer.Text = state.Deadline > 0 ? $"{Math.Ceiling(remaining):0}s" : "";
    }

    public override void _Input(InputEvent input)
    {
        if (input is not InputEventKey { Echo: false } key) return;
        if (key.Keycode == Key.Escape && key.Pressed) { _standings = false; _menu = !_menu; DrawState(); GetViewport().SetInputAsHandled(); }
        // Before GUI focus navigation consumes Tab. Inside the menu, Tab
        // remains available for moving between the name field and actions.
        if (key.Keycode == Key.Tab && !_menu) { _standings = key.Pressed; DrawState(); GetViewport().SetInputAsHandled(); }
    }

    public override void _Notification(int what)
    {
        // A released Tab cannot reach the game after Alt-Tab. Clear only that
        // temporary overlay; do not replace the current screen with a menu.
        if (what == NotificationApplicationFocusOut && _standings)
        { _standings = false; DrawState(); }
    }

    private void DrawState()
    {
        if (_state is not { } state) return;
        _drawnRevision = state.Revision; _needsRedraw = false;
        Clear(_roster);
        Clear(_stage); Clear(_actions);
        var self = state.Members.FirstOrDefault(m => m.PeerId == Multiplayer.GetUniqueId() && m.Connected);
        var host = Root.Session.IsServer;
        DrawRoster(state, self);
        _title.Text = state.Phase switch
        {
            PartyPhase.Lobby => "The party room", PartyPhase.Countdown => "Here we go!",
            PartyPhase.FinalResults => "The crown goes to…", PartyPhase.GameResults => "Points on the board",
            _ => "Rock · Paper · Scissors"
        };
        _subtitle.Text = state.Phase == PartyPhase.Lobby ? "Make yourself at home. Ready when you are."
            : state.Phase == PartyPhase.FinalResults ? "Same friends. Next party?"
            : $"Minigame 1 / 1    ·    Throw {Math.Max(1, state.Throw)} / {state.ThrowCount}";
        _status.Text = state.Phase == PartyPhase.Lobby && host && state.StartBlocker.Length > 0 ? LobbyBlocker(state) : state.Message;
        if (_standings) DrawStandings(state);
        else switch (state.Phase)
        {
            case PartyPhase.Lobby: DrawLobby(state, self, host); break;
            case PartyPhase.Countdown:
                Heading(_stage, "A little lucky chaos awaits"); Text(_stage, "The server starts everyone together. Get comfortable!"); break;
            case PartyPhase.Instructions:
                Heading(_stage, "One hand. Everyone else.");
                Text(_stage, "Choose Rock, Paper or Scissors. Your choice is locked and hidden until everyone submits or the timer ends.");
                Text(_stage, "Rock beats Scissors · Scissors beats Paper · Paper beats Rock.\nEarn one win against every hand you beat. A missed choice forfeits the throw.");
                Text(_stage, $"Most wins after {state.ThrowCount} throws takes first place.\nPlacement points: 10 / 6 / 3 / 1. Ties share points."); break;
            case PartyPhase.Choosing: DrawChoices(state, self); break;
            case PartyPhase.Reveal:
                Heading(_stage, "Show your hands!");
                foreach (var member in state.Members.Where(m => m.Playing))
                    Text(_stage, $"{member.Name}    ·    {(member.Connected ? member.Choice?.ToString() ?? "Missed this throw" : "Left the party")}    ·    {member.Wins} wins");
                break;
            case PartyPhase.GameResults: DrawStandings(state); break;
            case PartyPhase.FinalResults:
                var winners = state.Members.Where(m => state.Winners.Contains(m.PlayerId)).ToArray();
                var emblem = new TextureRect { Texture = GD.Load<Texture2D>("res://UI/Assets/Brand.svg"),
                    CustomMinimumSize = new Vector2(0, 90), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
                _stage.AddChild(emblem);
                Heading(_stage, winners.Length > 0 ? string.Join(" & ", winners.Select(m => m.Name)) : "No hands, no crown!");
                Text(_stage, winners.Length > 1 ? "Joint champions — a perfectly shared crown." : winners.Length == 1 ? "Your party champion." : "Nobody submitted a choice this party.");
                DrawStandings(state);
                if (host) Action(_actions, "Play again", () => Root.Session.PartyAction("again"));
                else Text(_actions, "Waiting for the host to play again.");
                break;
        }
        var menuButton = Action(_actions, "Menu", () => { _standings = false; _menu = true; DrawState(); });
        PartyUiStyle.Button(menuButton, true);
        if (_menu && _menuOverlay is null) DrawMenu();
        else if (!_menu) RemoveMenuOverlay();
    }

    private void DrawRoster(PartySnapshot state, PartyMemberView? self)
    {
        var heading = new HBoxContainer(); _roster.AddChild(heading);
        var playersTitle = Text(heading, "Players", 25); playersTitle.ThemeTypeVariation = "Heading";
        playersTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        Text(heading, $"{state.Members.Count(m => m.Connected && m.Role == PartyRole.Player)} / 8", 16);
        foreach (var role in new[] { PartyRole.Player, PartyRole.Spectator })
        {
            if (role == PartyRole.Spectator) Text(_roster, "SPECTATORS", 14);
            var shown = 0;
            foreach (var member in state.Members.Where(m => m.Role == role && (m.Connected || state.Phase != PartyPhase.Lobby)))
            {
                shown++;
                var card = Card(_roster, Colors.White, padding: 10);
                var row = new HBoxContainer(); card.AddChild(row);
                var dot = new ColorRect { Color = new Color(PartyPalette.Colors[member.ColorIndex]),
                    CustomMinimumSize = new Vector2(24, 24), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
                row.AddChild(dot);
                var label = Text(row, member.Name + (member.PlayerId == self?.PlayerId ? " (you)" : "") + (member.PeerId == 1 ? " · host" : ""), 17);
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                var badge = !member.Connected ? "Left" : state.Phase == PartyPhase.Lobby ? member.Ready ? "● Ready" : role == PartyRole.Player ? "Not ready" : "Watching"
                    : member.Playing ? $"{member.Score} pts · {member.Wins} wins" : "Watching";
                var stateLabel = Text(row, badge, 14);
                stateLabel.AddThemeColorOverride("font_color", member.Ready && state.Phase == PartyPhase.Lobby ? PartyUiStyle.Green : PartyUiStyle.Muted);
            }
            if (role == PartyRole.Spectator && shown == 0) Text(_roster, "No spectators yet", 14);
        }
    }

    private void DrawLobby(PartySnapshot state, PartyMemberView? self, bool host)
    {
        var game = Card(_stage, padding: 12);
        var gameBody = new VBoxContainer(); gameBody.AddThemeConstantOverride("separation", 6); game.AddChild(gameBody);
        Text(gameBody, "GAME 01 / 01", 14);
        Heading(gameBody, "Rock · Paper · Scissors", 28);
        Text(gameBody, $"Simultaneous choices  ·  {state.ThrowCount} throws  ·  2–8 players", 16);
        if (host)
        {
            var row = new HBoxContainer(); gameBody.AddChild(row); Text(row, "Throws per game", 16);
            var count = new SpinBox { MinValue = 1, MaxValue = 5, Step = 1, Value = state.ThrowCount };
            row.AddChild(count); count.ValueChanged += value => Root.Session.PartyAction("configure", (int)value);
        }
        var invite = Card(_stage, padding: 12);
        var inviteBody = new VBoxContainer(); inviteBody.AddThemeConstantOverride("separation", 6); invite.AddChild(inviteBody);
        Heading(inviteBody, host ? "Invite others" : "Connected to host", 22);
        var address = host ? Root.Session.HostAddresses.Split(' ')[0] : Root.Session.Address;
        var addressRow = new HBoxContainer(); inviteBody.AddChild(addressRow);
        var addressLabel = Text(addressRow, $"{address}:{Root.Session.Port}", 18);
        addressLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (host)
        {
            var copy = Action(addressRow, "Copy", () => DisplayServer.ClipboardSet($"{address}:{Root.Session.Port}"));
            PartyUiStyle.Button(copy, true);
            copy.TooltipText = "Copy address and UDP port";
        }
        if (self is null) return;
        var profile = Card(_stage, padding: 12);
        var profileBody = new VBoxContainer(); profileBody.AddThemeConstantOverride("separation", 6); profile.AddChild(profileBody);
        Heading(profileBody, "Your profile", 22);
        var colors = new HBoxContainer(); profileBody.AddChild(colors);
        Text(colors, "Color", 16);
        for (var i = 0; i < PartyPalette.Colors.Length; i++)
        {
            var index = i;
            var color = Action(colors, self.ColorIndex == i ? "✓" : "", () => Root.Session.PartyAction("color", index));
            color.CustomMinimumSize = new Vector2(42, 42);
            foreach (var style in new[] { "normal", "hover", "pressed" })
            {
                var face = (StyleBoxFlat)color.GetThemeStylebox(style).Duplicate();
                face.BgColor = new Color(PartyPalette.Colors[i]);
                face.ContentMarginLeft = face.ContentMarginRight = 8;
                face.BorderWidthLeft = face.BorderWidthTop = face.BorderWidthRight = face.BorderWidthBottom = style == "hover" ? 2 : 0;
                face.BorderColor = new Color("22263d");
                color.AddThemeStyleboxOverride(style, face);
            }
            color.TooltipText = $"Player color {i + 1}";
            color.Disabled = state.Members.Any(m => m.Connected && m.ColorIndex == index && m.PlayerId != self.PlayerId);
        }
        if (self.Role == PartyRole.Player)
        {
            var ready = Action(_actions, self.Ready ? "Unready" : "Ready up", () => Root.Session.PartyAction("ready", self.Ready ? 0 : 1));
            PartyUiStyle.Button(ready, self.Ready);
        }
        if (!host)
        {
            var role = Action(_actions, self.Role == PartyRole.Player ? "Spectate" : "Join players", () => Root.Session.PartyAction("role", self.Role == PartyRole.Player ? 1 : 0));
            PartyUiStyle.Button(role, true);
        }
        if (host)
        {
            var start = Action(_actions, "Start party", () => Root.Session.PartyAction("start"));
            start.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            start.Disabled = state.StartBlocker.Length > 0; start.TooltipText = LobbyBlocker(state);
        }
    }

    private static string LobbyBlocker(PartySnapshot state)
    {
        var players = state.Members.Where(m => m.Connected && m.Role == PartyRole.Player).ToArray();
        if (players.Length < 2) return "Invite at least one more player to start.";
        var waiting = players.Where(m => !m.Ready).Select(m => m.Name).ToArray();
        return waiting.Length > 0 ? $"Waiting for {string.Join(", ", waiting)} to ready up." : "";
    }

    private void DrawChoices(PartySnapshot state, PartyMemberView? self)
    {
        if (self?.Playing != true) { Heading(_stage, "Enjoy the show"); Text(_stage, "You're spectating this party. Join the players in the next lobby."); return; }
        Heading(_stage, state.YourChoice is { } hand ? $"{hand} locked in" : "What's your move?", 32);
        Text(_stage, state.YourChoice is not null ? "Your hand is hidden. Waiting for the other players…" : "Choose once. Other hands stay hidden until the reveal.");
        var row = new HBoxContainer(); _stage.AddChild(row);
        foreach (var choice in Enum.GetValues<RpsChoice>())
        {
            var button = Action(row, choice.ToString(), () => Root.Session.PartyAction("choice", (int)choice, state.PartyId, state.Throw));
            button.CustomMinimumSize = new Vector2(0, 110); button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Disabled = state.YourChoice is not null;
        }
        Text(_stage, $"{state.Members.Count(m => m.Connected && m.Playing && m.Submitted)} / {state.Members.Count(m => m.Connected && m.Playing)} hands locked", 16);
    }

    private void DrawStandings(PartySnapshot state)
    {
        Heading(_stage, "Standings", 28);
        foreach (var member in state.Members.Where(m => m.Playing).OrderByDescending(m => m.Score).ThenByDescending(m => m.Wins))
            Text(_stage, $"{member.Name}    ·    {member.Score} points    ·    {member.Wins} wins{(!member.Connected ? "    ·    departed" : "")}");
    }

    private void DrawMenu()
    {
        var overlay = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _shell.AddChild(overlay); _menuOverlay = overlay;
        var dim = new ColorRect { Color = new Color(0.08f, 0.12f, 0.21f, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); overlay.AddChild(dim);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); overlay.AddChild(margin);
        foreach (var side in new[] { "top", "bottom", "left", "right" }) margin.AddThemeConstantOverride("margin_" + side, 32);
        var row = new HBoxContainer(); margin.AddChild(row);
        row.AddSpacer(false);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(430, 0),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        panel.AddThemeStyleboxOverride("panel", PartyUiStyle.Box(PartyUiStyle.Cream, 24, padding: 26));
        row.AddChild(panel);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 20); panel.AddChild(body);
        Heading(body, "Party menu", 34);
        Text(body, "The game continues while this is open.", 16);
        var resume = Action(body, "Resume", () => { _menu = false; DrawState(); });
        resume.CustomMinimumSize = new Vector2(0, 62);
        var nameRow = new HBoxContainer(); body.AddChild(nameRow);
        var editRow = new HBoxContainer { Visible = false };
        var nameLabel = Text(nameRow, $"Display name   {Root.Session.LocalName}", 16);
        nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var edit = Action(nameRow, "Edit", () => { nameRow.Visible = false; editRow.Visible = true; });
        PartyUiStyle.Button(edit, true);
        body.AddChild(editRow);
        var name = new LineEdit { Text = Root.Session.LocalName, MaxLength = PlayerNames.MaxLength,
            PlaceholderText = "Display name", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        editRow.AddChild(name);
        var save = Action(editRow, "Save", () =>
        {
            Root.Session.ChangeName(name.Text);
            nameLabel.Text = $"Display name   {Root.Session.LocalName}";
            editRow.Visible = false; nameRow.Visible = true;
        });
        PartyUiStyle.Button(save, true);
        var divider = new HSeparator(); body.AddChild(divider);
        var confirm = new VBoxContainer { Visible = false };
        Button leave = null!;
        leave = Action(body, Root.Session.IsServer ? "Close hosted party" : "Leave party", () =>
        {
            if (!Root.Session.IsServer) { Root.Session.Leave("Left the party."); return; }
            leave.Visible = false;
            confirm.Visible = true;
        });
        PartyUiStyle.Button(leave, true);
        body.AddChild(confirm);
        Text(confirm, "Closing this room disconnects everyone.", 16);
        var close = Action(confirm, "Confirm close party", () => Root.Session.Leave("Hosted party closed."));
        PartyUiStyle.Button(close, true);
        var keep = Action(confirm, "Keep playing", () => { confirm.Visible = false; leave.Visible = true; });
        PartyUiStyle.Button(keep, true);
        Text(body, "Esc to close", 14);
    }

    private void RemoveMenuOverlay()
    {
        if (_menuOverlay is null) return;
        _shell.RemoveChild(_menuOverlay); _menuOverlay.QueueFree(); _menuOverlay = null;
    }

    private static PanelContainer Card(Node parent, Color? color = null, int padding = 17)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PartyUiStyle.Box(color ?? new Color("f3eee5"), 17, padding: padding));
        parent.AddChild(panel); return panel;
    }

    private static void Clear(Node node) { foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); } }
    private static Label Text(Node parent, string text, int size = 18)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", size); parent.AddChild(label); return label;
    }
    private static void Heading(Node parent, string text, int size = 36)
    { var label = Text(parent, text, size); label.ThemeTypeVariation = "Heading"; }
    private static Button Action(Node parent, string text, System.Action clicked)
    { var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.All }; parent.AddChild(button); button.Pressed += clicked; return button; }
}
