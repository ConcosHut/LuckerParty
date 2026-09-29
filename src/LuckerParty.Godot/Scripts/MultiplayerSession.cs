using Godot;
using LuckerParty.Core;

namespace LuckerParty.Godot;

public sealed class SessionPlayer
{
    public required string Name { get; set; }
    public required int Slot { get; init; }
    public FirstPersonPlayer? Avatar { get; init; }
    public SortedDictionary<int, NetworkInput> Inputs { get; } = new();
    public double LastRename { get; set; } = -1000;
    public double LastInputAt { get; set; }
    public int PacketsThisTick { get; set; }
    public double LastPartyRequest { get; set; } = -1000;
}

// All RPCs stay at /root/Main/Network, including through menu/leave/rejoin.
public partial class MultiplayerSession : Node
{
    public const int Protocol = 2, MaxPlayers = 8, DefaultPort = 27015;
    private const string ArenaRevision = "sandbox-v1";
    private ENetMultiplayerPeer? _peer;
    private readonly Dictionary<int, SessionPlayer> _players = new();
    private readonly Dictionary<int, double> _awaitingRegistration = new();
    private double _connectionDeadline, _lastSnapshotAt;
    private int _tick, _lastSnapshotTick;
    private byte[]? _snapshot;
    public GameRoot OwnerRoot { get; set; } = null!;
    public NetworkAutomation? Automation { get; set; }
    public IReadOnlyDictionary<int, SessionPlayer> Players => _players;
    public bool Active { get; private set; }
    public bool IsServer => Active && Multiplayer.IsServer();
    public bool Dedicated { get; private set; }
    public string LocalName { get; private set; } = "Player";
    public int Port { get; private set; } = DefaultPort;
    public string Address { get; private set; } = "";
    public string Status { get; private set; } = "Main menu";
    public string HostAddresses { get; private set; } = "";
    public string Description => IsServer ? $"Hosting on port {Port}  /  {_players.Count}/{MaxPlayers} players\nJoin at {HostAddresses}"
        : Active ? $"{Address}:{Port}  /  {_players.Count}/{MaxPlayers} players" : Status;
    private static double Now => Time.GetTicksMsec() / 1000d;

    public override void _Ready()
    {
        ProcessPhysicsPriority = 100;
        Multiplayer.PeerConnected += PeerConnected;
        Multiplayer.PeerDisconnected += PeerDisconnected;
        Multiplayer.ConnectedToServer += Connected;
        Multiplayer.ConnectionFailed += ConnectionFailed;
        Multiplayer.ServerDisconnected += ServerDisconnected;
    }

    public bool Host(string name, int port, bool dedicated = false, bool party = false)
    {
        if (Active) return false;
        if (party && dedicated) { OwnerRoot.SetMessage("Party mode needs a listen host. Use --host --party."); return false; }
        if (port is < 1024 or > 65535) { OwnerRoot.SetMessage("Choose a port from 1024 to 65535."); return false; }
        var peer = new ENetMultiplayerPeer();
        peer.SetBindIP("*");
        var error = peer.CreateServer(port, dedicated ? MaxPlayers : MaxPlayers - 1, 3);
        if (error != Error.Ok)
        {
            peer.Dispose(); OwnerRoot.SetMessage($"Cannot host on port {port}. It may already be in use ({error}).");
            Status = "Host failed"; return false;
        }
        Begin(peer, name, port, dedicated);
        IsParty = party;
        if (party) InitializeParty();
        HostAddresses = string.Join(" or ", IP.GetLocalAddresses().Where(address =>
            System.Net.IPAddress.TryParse(address, out var ip)
            && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && !System.Net.IPAddress.IsLoopback(ip)).Take(3));
        if (HostAddresses.Length == 0) HostAddresses = "127.0.0.1 (same computer)";
        Address = "0.0.0.0"; Status = "Hosting";
        if (party) OwnerRoot.EnterParty(); else OwnerRoot.EnterArena();
        if (!dedicated) SpawnPeer(1, LocalName, 0);
        GD.Print($"NET_HOST: port={Port} dedicated={Dedicated} protocol={Protocol}");
        return true;
    }

    public bool Join(string name, string address, int port)
    {
        if (Active) return false;
        address = address.Trim();
        if (address.Length is 0 or > 253 || address.Any(char.IsWhiteSpace) || port is < 1024 or > 65535)
        { OwnerRoot.SetMessage("Enter an IP address or hostname and a port from 1024 to 65535."); return false; }
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(address, port, 3);
        if (error != Error.Ok)
        { peer.Dispose(); OwnerRoot.SetMessage($"Could not connect ({error}). Check the address."); Status = "Join failed"; return false; }
        Begin(peer, name, port, false);
        Address = address; Status = "Connecting";
        _connectionDeadline = Now + 8;
        OwnerRoot.SetConnecting($"Connecting to {address}:{port}…");
        return true;
    }

    private void Begin(ENetMultiplayerPeer peer, string name, int port, bool dedicated)
    {
        _peer = peer; Active = true; Dedicated = dedicated; LocalName = PlayerNames.Clean(name); Port = port;
        _tick = 0; _lastSnapshotTick = 0; _snapshot = null; _lastSnapshotAt = Now;
        Engine.PhysicsTicksPerSecond = 60;
        Multiplayer.MultiplayerPeer = peer;
        OwnerRoot.SaveName(LocalName);
    }

    private void Connected()
    {
        if (!Active) return;
        Status = "Joining";
        RpcId(1, MethodName.Register, Automation?.RequestedProtocol ?? Protocol, ArenaRevision, LocalName);
    }

    private void PeerConnected(long id)
    {
        if (IsServer) _awaitingRegistration[(int)id] = Now;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Register(int protocol, string arena, string name)
    {
        if (!IsServer) return;
        var id = Multiplayer.GetRemoteSenderId();
        if (!_awaitingRegistration.Remove(id) || _players.ContainsKey(id)) return;
        if (protocol != Protocol || arena != ArenaRevision || name is null || name.Length > 128 || _players.Count >= MaxPlayers)
        {
            RpcId(id, MethodName.Rejected, "This lobby uses a different game protocol or is full. Update both games and try again.");
            DisconnectLater(id); return;
        }
        RpcId(id, MethodName.Welcome, IsParty);
        foreach (var member in _players)
            RpcId(id, MethodName.SpawnPeer, member.Key, member.Value.Name, member.Value.Slot);
        var slot = Enumerable.Range(0, MaxPlayers).First(s => _players.Values.All(p => p.Slot != s));
        name = PlayerNames.Clean(name);
        SpawnPeer(id, name, slot);
        Rpc(MethodName.SpawnPeer, id, name, slot);
        if (IsParty) PublishParty();
        GD.Print($"NET_JOIN: id={id} name={name} players={_players.Count}");
    }

    private async void DisconnectLater(int id)
    {
        await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout);
        if (IsServer && _peer is not null && Multiplayer.GetPeers().Contains(id)) _peer.DisconnectPeer(id);
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Rejected(string reason) { if (Active && !IsServer) Leave(reason); }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SpawnPeer(int id, string name, int slot)
    {
        if (!Active || slot is < 0 or >= MaxPlayers || id <= 0) return;
        if (id == Multiplayer.GetUniqueId())
        {
            if (LocalName != name) { LocalName = name; OwnerRoot.SaveName(name); }
            OwnerRoot.World?.SetLocalName(name);
        }
        if (_players.TryGetValue(id, out var existing))
        {
            existing.Name = name; existing.Avatar?.SetDisplayName(name);
            if (IsServer && IsParty) { _partyRules!.Rename(id, name); PublishParty(); }
            return;
        }
        FirstPersonPlayer? avatar = null;
        if (!IsParty)
        {
            OwnerRoot.EnterArena();
            var spawn = new Vector3(-10 + (slot % 4) * 6, .05f, 10 - (slot / 4) * 4);
            avatar = OwnerRoot.World!.SpawnPlayer(id, name, spawn);
        }
        _players[id] = new() { Name = name, Slot = slot, Avatar = avatar, LastInputAt = Now };
        if (IsServer && IsParty) { _partyRules!.Join(id, name, Now); PublishParty(); }
        if (id == Multiplayer.GetUniqueId())
        { Status = "Connected"; _connectionDeadline = 0; _lastSnapshotAt = Now; }
        GD.Print($"NET_MEMBER: id={id} name={name} local={id == Multiplayer.GetUniqueId()}");
    }

    private void PeerDisconnected(long id)
    {
        if (!IsServer) return;
        _awaitingRegistration.Remove((int)id);
        DespawnPeer((int)id);
        Rpc(MethodName.DespawnPeer, (int)id);
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DespawnPeer(int id)
    {
        if (!_players.Remove(id, out var player)) return;
        player.Avatar?.SetPhysicsProcess(false); player.Avatar?.QueueFree();
        if (IsServer && IsParty) { _partyRules!.Leave(id, Now); PublishParty(); }
        GD.Print($"NET_LEFT: id={id} players={_players.Count}");
    }

    public void ChangeName(string name)
    {
        LocalName = PlayerNames.Clean(name); OwnerRoot.SaveName(LocalName);
        if (!Active) return;
        if (IsServer) RenamePeer(1, LocalName);
        else RpcId(1, MethodName.RequestName, LocalName);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestName(string name)
    {
        if (!IsServer || name is null || name.Length > 128) return;
        var id = Multiplayer.GetRemoteSenderId();
        if (!_players.TryGetValue(id, out var player)) return;
        if (Now - player.LastRename < 1)
        {
            RpcId(id, MethodName.SpawnPeer, id, player.Name, player.Slot);
            return;
        }
        player.LastRename = Now;
        RenamePeer(id, PlayerNames.Clean(name));
    }

    private void RenamePeer(int id, string name)
    {
        if (!_players.TryGetValue(id, out var player)) return;
        SpawnPeer(id, name, player.Slot);
        Rpc(MethodName.SpawnPeer, id, name, player.Slot);
    }

    public void SendInputs(byte[] batch)
    {
        if (Active && !IsServer) RpcId(1, MethodName.SubmitInputs, batch);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 1)]
    private void SubmitInputs(byte[] batch)
    {
        if (IsParty || !IsServer || !_players.TryGetValue(Multiplayer.GetRemoteSenderId(), out var player)
            || player.Avatar is null
            || ++player.PacketsThisTick > 8) return;
        try
        {
            var commands = InputCodec.Decode(batch);
            if (commands.Length > 0) player.LastInputAt = Now;
            foreach (var input in commands)
                if (input.Sequence > player.Avatar.LastInput && input.Sequence <= player.Avatar.LastInput + 600)
                    player.Inputs.TryAdd(input.Sequence, input);
            while (player.Inputs.Count > 120) player.Inputs.Remove(player.Inputs.First().Key);
        }
        catch (InvalidDataException) { /* Ignore malformed input; never use client transforms. */ }
    }

    public NetworkInput[] ConsumeInputs(int id)
    {
        var queue = _players[id].Inputs;
        // One command per server physics tick: sending faster cannot increase speed.
        // Retain a bounded queue so ordinary burst delivery does not discard actions.
        if (queue.Count == 0) return Array.Empty<NetworkInput>();
        var first = queue.First(); queue.Remove(first.Key);
        return new[] { first.Value };
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Active) return;
        _tick++;
        TickParty();
        if (IsServer)
        {
            foreach (var player in _players.Values) player.PacketsThisTick = 0;
            foreach (var pending in _awaitingRegistration.Where(p => Now - p.Value > 10).ToArray())
            { _peer!.DisconnectPeer(pending.Key); _awaitingRegistration.Remove(pending.Key); }
            foreach (var silent in _players.Where(p => p.Key != 1 && Now - p.Value.LastInputAt > 8).ToArray())
            {
                if (Multiplayer.GetPeers().Contains(silent.Key)) _peer!.DisconnectPeer(silent.Key);
                DespawnPeer(silent.Key); Rpc(MethodName.DespawnPeer, silent.Key);
            }
            if (!IsParty && _tick % 2 == 0 && Multiplayer.GetPeers().Length > 0)
                Rpc(MethodName.Snapshot, EncodeSnapshot());
        }
        else
        {
            if (_snapshot is { } snapshot) { _snapshot = null; ApplySnapshot(snapshot); }
            if ((_connectionDeadline > 0 && Now > _connectionDeadline)
                || (_connectionDeadline == 0 && Now - _lastSnapshotAt > 8))
                Leave("Connection timed out. Check the address, host, UDP port, and firewall.");
        }
    }

    private byte[] EncodeSnapshot()
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(_tick); writer.Write((byte)_players.Count);
        foreach (var member in _players)
        {
            var s = member.Value.Avatar!.State;
            writer.Write(member.Key); writer.Write(s.Ack);
            writer.Write(s.Position.X); writer.Write(s.Position.Y); writer.Write(s.Position.Z);
            writer.Write(s.Velocity.X); writer.Write(s.Velocity.Y); writer.Write(s.Velocity.Z);
            writer.Write(s.Yaw); writer.Write(s.Pitch); writer.Write(s.Grounded);
        }
        return stream.ToArray();
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 2)]
    private void Snapshot(byte[] bytes)
    {
        if (Active && !IsServer && bytes is not null && bytes.Length <= 5 + MaxPlayers * 41) _snapshot = bytes;
    }

    private void ApplySnapshot(byte[] bytes)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(bytes));
            var tick = reader.ReadInt32(); var count = reader.ReadByte();
            if (tick <= _lastSnapshotTick || count > MaxPlayers || bytes.Length != 5 + count * 41) return;
            _lastSnapshotTick = tick; _lastSnapshotAt = Now;
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt32(); var ack = reader.ReadInt32();
                var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var velocity = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var state = new PlayerState(position, velocity, reader.ReadSingle(), reader.ReadSingle(), reader.ReadBoolean(), ack);
                if (_players.TryGetValue(id, out var player)) player.Avatar?.ReceiveState(state);
            }
        }
        catch (EndOfStreamException) { }
    }

    public void Leave(string message)
    {
        Active = false; Status = message;
        _peer?.Close(); Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
        _peer?.Dispose(); _peer = null;
        _players.Clear(); _awaitingRegistration.Clear(); _snapshot = null;
        _connectionDeadline = 0;
        IsParty = false; _partyRules = null; PartyState = null;
        OwnerRoot.ReturnToMenu(message);
        GD.Print($"NET_ENDED: {message}");
    }
    private void ConnectionFailed() { if (Active) Leave("Could not reach the host. Check its IP, UDP port, and firewall."); }
    private void ServerDisconnected() { if (Active) Leave("The host closed the lobby or disconnected."); }
    public override void _ExitTree()
    {
        Active = false;
        Multiplayer.PeerConnected -= PeerConnected; Multiplayer.PeerDisconnected -= PeerDisconnected;
        Multiplayer.ConnectedToServer -= Connected; Multiplayer.ConnectionFailed -= ConnectionFailed;
        Multiplayer.ServerDisconnected -= ServerDisconnected;
        _peer?.Close(); _peer?.Dispose();
    }
}
