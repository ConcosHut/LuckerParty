namespace LuckerParty.Core;

/// <summary>Authoritative party rules. No engine objects, networking or wall clock.</summary>
public sealed class PartyCoordinator
{
    private sealed class Member(long playerId, int peerId, string name, int color)
    {
        public long PlayerId = playerId;
        public int PeerId = peerId;
        public string Name = name;
        public int Color = color;
        public PartyRole Role;
        public bool Ready, Connected = true, Playing, Participated;
        public int Score, Wins, LastSequence;
        public RpsChoice? Hand, Revealed;
    }

    private readonly List<Member> _members = [];
    private readonly PartyTiming _timing;
    private long _nextPlayerId, _revision;
    public int HostPeerId { get; }
    public PartyPhase Phase { get; private set; } = PartyPhase.Lobby;
    public long PartyId { get; private set; }
    public int Throw { get; private set; }
    public int ThrowCount { get; private set; } = 3;
    public double Deadline { get; private set; }
    public string Message { get; private set; } = "Ready up for a little lucky chaos.";

    public PartyCoordinator(int hostPeerId = 1, PartyTiming? timing = null)
    { HostPeerId = hostPeerId; _timing = timing ?? new(); }

    public void Join(int peerId, string name, double now)
    {
        if (peerId <= 0 || Find(peerId) is not null || _members.Count(m => m.Connected) >= 8) return;
        _members.RemoveAll(m => !m.Connected && (Phase == PartyPhase.Lobby || !m.Playing));
        var used = _members.Where(m => m.Connected).Select(m => m.Color).ToHashSet();
        var color = Enumerable.Range(0, PartyPalette.Colors.Length).First(c => !used.Contains(c));
        var member = new Member(++_nextPlayerId, peerId, PlayerNames.Clean(name), color);
        if (Phase != PartyPhase.Lobby) member.Role = PartyRole.Spectator;
        _members.Add(member);
        if (Phase == PartyPhase.Countdown) CancelCountdown("The roster changed. Ready up again.");
        _revision++;
    }

    public void Leave(int peerId, double now)
    {
        var member = Find(peerId); if (member is null) return;
        member.Connected = member.Ready = false;
        if (Phase == PartyPhase.Countdown) CancelCountdown("A player left. Ready up again.");
        else if (Phase is not (PartyPhase.Lobby or PartyPhase.FinalResults) && ActivePlayers().Count() < 2)
            ReturnToLobby("Party cancelled: at least two players are needed.");
        _revision++;
        Advance(now);
    }

    public void Rename(int peerId, string name)
    { if (Find(peerId) is { } member) { member.Name = PlayerNames.Clean(name); _revision++; } }

    public bool SetReady(int peerId, bool ready)
    {
        if (Phase != PartyPhase.Lobby || Find(peerId) is not { Role: PartyRole.Player } member) return false;
        if (member.Ready == ready) return false;
        member.Ready = ready; _revision++; return true;
    }

    public bool SetRole(int peerId, PartyRole role)
    {
        if (Phase != PartyPhase.Lobby || !Enum.IsDefined(role) || Find(peerId) is not { } member ||
            member.Role == role || peerId == HostPeerId && role == PartyRole.Spectator) return false;
        member.Role = role; member.Ready = false; _revision++; return true;
    }

    public bool SetColor(int peerId, int color)
    {
        if (Phase != PartyPhase.Lobby || color < 0 || color >= PartyPalette.Colors.Length || Find(peerId) is not { } member ||
            _members.Any(m => m.Connected && m.PeerId != peerId && m.Color == color) || member.Color == color) return false;
        member.Color = color; _revision++; return true;
    }

    public bool Configure(int peerId, int throws)
    {
        if (peerId != HostPeerId || Phase != PartyPhase.Lobby || throws is < 1 or > 5 || throws == ThrowCount) return false;
        ThrowCount = throws;
        foreach (var member in _members) member.Ready = false;
        Message = "Party settings changed. Ready up again."; _revision++; return true;
    }

    public string StartBlocker
    {
        get
        {
            if (Phase != PartyPhase.Lobby) return "A party is already in progress.";
            var players = _members.Where(m => m.Connected && m.Role == PartyRole.Player).ToArray();
            if (players.Length < 2) return "Invite at least one more player.";
            if (players.Any(m => !m.Ready)) return "Waiting for every player to ready up.";
            return "";
        }
    }

    public bool Start(int peerId, double now)
    {
        if (peerId != HostPeerId || StartBlocker.Length != 0) return false;
        _members.RemoveAll(m => !m.Connected);
        PartyId++; Throw = 0;
        foreach (var member in _members)
        {
            member.Playing = member.Role == PartyRole.Player;
            member.Score = member.Wins = member.LastSequence = 0;
            member.Participated = false; member.Hand = member.Revealed = null;
        }
        Transition(PartyPhase.Countdown, now + _timing.Countdown, "Party starts soon…");
        return true;
    }

    public bool Submit(int peerId, long partyId, int attempt, int sequence, RpsChoice choice, double now)
    {
        // Deadline validation uses authority time even if Advance hasn't run this frame yet.
        if (Phase != PartyPhase.Choosing || now >= Deadline || PartyId != partyId || Throw != attempt ||
            !Enum.IsDefined(choice) || sequence <= 0 || sequence > 1_000_000 || Find(peerId) is not { Playing: true } member ||
            sequence <= member.LastSequence || member.Hand is not null) return false;
        member.LastSequence = sequence; member.Hand = choice; member.Participated = true; _revision++;
        Advance(now); return true;
    }

    public bool PlayAgain(int peerId)
    {
        if (peerId != HostPeerId || Phase != PartyPhase.FinalResults) return false;
        ReturnToLobby("Another party? Ready up when you're set."); return true;
    }

    public void Advance(double now)
    {
        if (Phase == PartyPhase.Choosing && (now >= Deadline || ActivePlayers().All(m => m.Hand is not null)))
        {
            ResolveThrow();
            Transition(PartyPhase.Reveal, now + _timing.Reveal, "Hands revealed!");
        }
        else if (Deadline > 0 && now >= Deadline)
        {
            switch (Phase)
            {
                case PartyPhase.Countdown:
                    Transition(PartyPhase.Instructions, now + _timing.Instructions, "Rock beats scissors. Scissors beats paper. Paper beats rock."); break;
                case PartyPhase.Instructions: BeginThrow(now); break;
                case PartyPhase.Reveal:
                    if (Throw < ThrowCount) BeginThrow(now);
                    else { AwardPoints(); Transition(PartyPhase.GameResults, now + _timing.Results, "Minigame complete — points awarded."); }
                    break;
                case PartyPhase.GameResults:
                    Transition(PartyPhase.FinalResults, 0, "Your party champions"); break;
            }
        }
    }

    public PartySnapshot SnapshotFor(int peerId, double now)
    {
        var reveal = Phase is PartyPhase.Reveal or PartyPhase.GameResults or PartyPhase.FinalResults;
        var members = _members.Select(m => new PartyMemberView(m.PlayerId, m.PeerId, m.Name, m.Color, m.Role,
            m.Ready, m.Connected, m.Playing, m.Score, m.Wins, m.Participated, m.Hand is not null, reveal ? m.Revealed : null)).ToArray();
        var finalists = _members.Where(m => m.Connected && m.Playing && m.Participated).ToArray();
        var winners = Phase == PartyPhase.FinalResults && finalists.Length > 0
            ? finalists.Where(m => m.Score == finalists.Max(p => p.Score)).Select(m => m.PlayerId).ToArray() : [];
        return new(_revision, PartyId, Phase, now, Deadline, Throw, ThrowCount, Message, StartBlocker, members,
            winners, Find(peerId)?.Hand);
    }

    public static bool Beats(RpsChoice hand, RpsChoice other) => (hand, other) is
        (RpsChoice.Rock, RpsChoice.Scissors) or (RpsChoice.Paper, RpsChoice.Rock) or (RpsChoice.Scissors, RpsChoice.Paper);

    private Member? Find(int peerId) => _members.FirstOrDefault(m => m.PeerId == peerId && m.Connected);
    private IEnumerable<Member> ActivePlayers() => _members.Where(m => m.Connected && m.Playing);
    private void BeginThrow(double now)
    {
        Throw++;
        foreach (var member in _members) member.Hand = member.Revealed = null;
        Transition(PartyPhase.Choosing, now + _timing.Choice, "Pick a hand. Your choice stays hidden until everyone locks in or time runs out.");
    }
    private void ResolveThrow()
    {
        var players = ActivePlayers().ToArray();
        foreach (var member in players)
        {
            member.Revealed = member.Hand;
            if (member.Hand is not { } hand) continue;
            member.Wins += players.Count(other => other != member && (other.Hand is null || Beats(hand, other.Hand.Value)));
        }
    }
    private void AwardPoints()
    {
        var players = ActivePlayers().Where(m => m.Participated).ToArray();
        foreach (var member in players)
        {
            var rank = 1 + players.Count(other => other.Wins > member.Wins);
            member.Score = rank switch { 1 => 10, 2 => 6, 3 => 3, _ => 1 };
        }
    }
    private void CancelCountdown(string reason)
    { foreach (var member in _members) member.Ready = false; ReturnToLobby(reason); }
    private void ReturnToLobby(string reason)
    {
        foreach (var member in _members) { member.Ready = member.Playing = false; member.Hand = member.Revealed = null; }
        Transition(PartyPhase.Lobby, 0, reason);
    }
    private void Transition(PartyPhase phase, double deadline, string message)
    { Phase = phase; Deadline = deadline; Message = message; _revision++; }
}
