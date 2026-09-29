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
    private PartySnapshot? _state;
    private long _drawnRevision = -1;
    private double _receivedAt;
    private bool _menu, _standings, _needsRedraw, _renderedMenu;
    public bool MenuOpen => _menu;
    public bool StandingsVisible => _standings && !_menu;

    public override void _Ready()
    {
        _shell = GD.Load<PackedScene>("res://UI/PartyShell.tscn").Instantiate<Control>(); AddChild(_shell);
        _roster = _shell.GetNode<VBoxContainer>("Margin/Column/Body/RosterPanel/Scroll/Roster");
        _stage = _shell.GetNode<VBoxContainer>("Margin/Column/Body/StagePanel/Scroll/Stage");
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
    { _menu = _standings = false; DrawState(); }

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
    { if (what == NotificationApplicationFocusOut) { _menu = true; _standings = false; if (_state is not null) DrawState(); } }

    private void DrawState()
    {
        if (_state is not { } state) return;
        _drawnRevision = state.Revision; _needsRedraw = false;
        // Keep an open menu's fields and keyboard focus alive during roster
        // changes. Editing a name must not be interrupted by another player.
        var preserveMenu = _menu && _renderedMenu;
        Clear(_roster);
        if (!preserveMenu) { Clear(_stage); Clear(_actions); }
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
        _status.Text = state.Phase == PartyPhase.Lobby && host && state.StartBlocker.Length > 0 ? state.StartBlocker : state.Message;
        if (preserveMenu) return;
        _renderedMenu = _menu;
        if (_menu) DrawMenu();
        else if (_standings) DrawStandings(state);
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
        Action(_actions, _menu ? "Resume" : "Menu", () => { _standings = false; _menu = !_menu; DrawState(); });
    }

    private void DrawRoster(PartySnapshot state, PartyMemberView? self)
    {
        Heading(_roster, "Your party", 26);
        foreach (var role in new[] { PartyRole.Player, PartyRole.Spectator })
        {
            Text(_roster, role == PartyRole.Player ? "PLAYERS" : "SPECTATORS", 13);
            foreach (var member in state.Members.Where(m => m.Role == role && (m.Connected || state.Phase != PartyPhase.Lobby)))
            {
                var row = new HBoxContainer(); _roster.AddChild(row);
                var dot = new ColorRect { Color = new Color(PartyPalette.Colors[member.ColorIndex]),
                    CustomMinimumSize = new Vector2(12, 12), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
                row.AddChild(dot);
                var label = Text(row, member.Name + (member.PlayerId == self?.PlayerId ? " (you)" : "") + (member.PeerId == 1 ? " · host" : ""));
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                Text(_roster, !member.Connected ? "Left the party" : state.Phase == PartyPhase.Lobby ? member.Ready ? "Ready" : role == PartyRole.Player ? "Getting ready" : "Watching"
                    : member.Playing ? $"{member.Score} points · {member.Wins} wins" : "Watching this party", 14);
            }
        }
    }

    private void DrawLobby(PartySnapshot state, PartyMemberView? self, bool host)
    {
        Heading(_stage, "Tonight's playlist", 30);
        Heading(_stage, "01   Rock · Paper · Scissors", 26);
        Text(_stage, $"Simultaneous choices  ·  {state.ThrowCount} throws  ·  2–8 players");
        if (host)
        {
            var row = new HBoxContainer(); _stage.AddChild(row); Text(row, "Throws per game");
            var count = new SpinBox { MinValue = 1, MaxValue = 5, Step = 1, Value = state.ThrowCount };
            row.AddChild(count); count.ValueChanged += value => Root.Session.PartyAction("configure", (int)value);
            Action(_stage, "Copy host IP", () => DisplayServer.ClipboardSet(Root.Session.HostAddresses.Split(' ')[0]));
            Text(_stage, $"Join at {Root.Session.HostAddresses}\nUDP port {Root.Session.Port}", 14);
        }
        else Text(_stage, $"Connected to {Root.Session.Address}:{Root.Session.Port}");
        if (self is null) return;
        var colors = new HBoxContainer(); _stage.AddChild(colors);
        Text(colors, "Your color");
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
        if (self.Role == PartyRole.Player) Action(_actions, self.Ready ? "Unready" : "Ready up", () => Root.Session.PartyAction("ready", self.Ready ? 0 : 1));
        if (!host) Action(_actions, self.Role == PartyRole.Player ? "Spectate" : "Join players", () => Root.Session.PartyAction("role", self.Role == PartyRole.Player ? 1 : 0));
        if (host)
        {
            var start = Action(_actions, "Start party", () => Root.Session.PartyAction("start"));
            start.Disabled = state.StartBlocker.Length > 0; start.TooltipText = state.StartBlocker;
        }
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
        Heading(_stage, "Take a breath"); Text(_stage, "The party keeps playing while this menu is open.");
        var name = new LineEdit { Text = Root.Session.LocalName, MaxLength = PlayerNames.MaxLength, PlaceholderText = "Display name" }; _stage.AddChild(name);
        Action(_stage, "Save name", () => Root.Session.ChangeName(name.Text));
        Action(_stage, "Resume party", () => { _menu = false; DrawState(); });
        Action(_stage, Root.Session.IsServer ? "Close hosted party" : "Leave party", () => Root.Session.Leave("Left the party."));
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
