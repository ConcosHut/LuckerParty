using Godot;
using LuckerParty.Core;
using System.Text.Json;

namespace LuckerParty.Godot;

public partial class MultiplayerSession
{
    private PartyCoordinator? _partyRules;
    private long _publishedRevision = -1;
    private int _partySequence;
    public bool IsParty { get; private set; }
    public PartySnapshot? PartyState { get; private set; }
    public event Action<PartySnapshot>? PartyChanged;

    private void InitializeParty()
    {
        // Only the machine-local scenario driver can shorten clocks for checks.
        _partyRules = new PartyCoordinator(timing: Automation?.FastParty == true ? new PartyTiming(.4, .5, 4, .6, .7) : null);
        _publishedRevision = -1; _partySequence = 0;
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Welcome(bool party)
    {
        if (!Active || IsServer) return;
        IsParty = party; _partySequence = 0; PartyState = null;
        if (party) OwnerRoot.EnterParty(); else OwnerRoot.EnterArena();
    }

    public void PartyAction(string action, int value = 0, long? partyId = null, int? attempt = null)
    {
        if (!Active || !IsParty) return;
        var party = partyId ?? PartyState?.PartyId ?? 0;
        var turn = attempt ?? PartyState?.Throw ?? 0;
        if (IsServer) HandlePartyAction(1, action, value, party, turn, ++_partySequence);
        else RpcId(1, MethodName.RequestPartyAction, action, value, party, turn, ++_partySequence);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestPartyAction(string action, int value, long party, int turn, int sequence)
    {
        if (!IsServer || !IsParty || action is null || action.Length > 24) return;
        var sender = Multiplayer.GetRemoteSenderId();
        if (!_players.TryGetValue(sender, out var member) || Now - member.LastPartyRequest < .05) return;
        member.LastPartyRequest = Now;
        HandlePartyAction(sender, action, value, party, turn, sequence);
    }

    private void HandlePartyAction(int sender, string action, int value, long party, int turn, int sequence)
    {
        var rules = _partyRules!;
        var accepted = action switch
        {
            "ready" => value is 0 or 1 && rules.SetReady(sender, value == 1),
            "role" => rules.SetRole(sender, (PartyRole)value),
            "color" => rules.SetColor(sender, value),
            "configure" => rules.Configure(sender, value),
            "start" => rules.Start(sender, Now),
            "choice" => rules.Submit(sender, party, turn, sequence, (RpsChoice)value, Now),
            "again" => rules.PlayAgain(sender),
            _ => false
        };
        if (accepted) GD.Print($"PARTY_COMMAND: peer={sender} action={action} party={rules.PartyId} phase={rules.Phase}");
        // Republish accepted state even on a rejected request, restoring authoritative UI.
        PublishParty();
    }

    private void TickParty()
    {
        if (!IsParty) return;
        if (IsServer)
        {
            _partyRules!.Advance(Now);
            var state = _partyRules.SnapshotFor(1, Now);
            if (state.Revision != _publishedRevision || _tick % 60 == 0) PublishParty();
        }
        else if (_tick % 60 == 0) RpcId(1, MethodName.PartyHeartbeat);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PartyHeartbeat()
    {
        if (IsServer && IsParty && _players.TryGetValue(Multiplayer.GetRemoteSenderId(), out var member)) member.LastInputAt = Now;
    }

    private void PublishParty()
    {
        if (!IsServer || !IsParty) return;
        var state = _partyRules!.SnapshotFor(1, Now);
        if (PartyState?.Phase != state.Phase)
        {
            GD.Print($"PARTY_PHASE: party={state.PartyId} phase={state.Phase} throw={state.Throw} scores={string.Join(',', state.Members.Select(m => $"{m.PlayerId}:{m.Score}"))}");
            if (state.Phase == PartyPhase.GameResults)
                foreach (var member in state.Members.Where(m => m.Playing))
                    GD.Print($"PARTY_RESULT: party={state.PartyId} player={member.PlayerId} wins={member.Wins} award={member.Score} reason={(!member.Connected ? "departed" : !member.Participated ? "no-participation" : "rps-placement")}");
        }
        AcceptPartyState(state);
        _publishedRevision = state.Revision;
        foreach (var peer in _players.Keys.Where(id => id != 1))
            RpcId(peer, MethodName.ReceiveParty, JsonSerializer.SerializeToUtf8Bytes(_partyRules.SnapshotFor(peer, Now)));
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveParty(byte[] bytes)
    {
        if (!Active || IsServer || !IsParty || bytes is null || bytes.Length > 16384) return;
        try
        {
            if (JsonSerializer.Deserialize<PartySnapshot>(bytes) is { } state && state.Members.Length <= 16 &&
                (PartyState is null || state.Revision >= PartyState.Revision))
            { _lastSnapshotAt = Now; AcceptPartyState(state); }
        }
        catch (JsonException) { }
    }

    private void AcceptPartyState(PartySnapshot state)
    { PartyState = state; PartyChanged?.Invoke(state); }
}
