using Godot;
using LuckerParty.Core;
using System.Text.Json;

namespace LuckerParty.Godot;

// Bounded external scenario driver. Normal player launches never read control files.
public partial class NetworkAutomation : Node
{
    public GameRoot Root { get; set; } = null!;
    public string? ControlPath { get; set; }
    public string? ProbePath { get; set; }
    public double ExitAfter { get; set; }
    private double _elapsed, _poll;
    private int _revision = -1;
    private ControlMessage _control = new();
    private bool _jump, _reset;
    private int _cameraFrames, _cameraStationary;
    private float _largestCameraStep;
    private Vector3? _lastCamera;
    public override void _Ready() { ProcessPriority = 200; }
    private readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    public int RequestedProtocol => _control.Protocol ?? MultiplayerSession.Protocol;

    public NetworkInput Input(NetworkInput original)
    {
        if (ControlPath is null) return original;
        var local = Root.Session.Players.Values.FirstOrDefault(p => p.Avatar.LocalControl);
        if (local is not null) local.Avatar.InterpolateCameraPosition = !_control.RawCamera;
        var command = original with { X = _control.X, Y = _control.Y, Yaw = _control.Yaw,
            Pitch = _control.Pitch, Sprint = _control.Sprint, Jump = _jump, Reset = _reset };
        _jump = false; _reset = false;
        return command;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta; _poll += delta;
        var local = Root.Session.Players.Values.FirstOrDefault(p => p.Avatar.LocalControl)?.Avatar;
        if (local is not null && Math.Abs(_control.X) + Math.Abs(_control.Y) > .1f)
        {
            if (_lastCamera is { } previous)
            {
                var step = previous.DistanceTo(local.CameraPosition);
                _cameraFrames++; if (step < .000001f) _cameraStationary++;
                _largestCameraStep = Math.Max(_largestCameraStep, step);
            }
            _lastCamera = local.CameraPosition;
        }
        else _lastCamera = null;
        if (_poll < .1) return;
        _poll = 0;
        if (ControlPath is not null && File.Exists(ControlPath))
        {
            try
            {
                var message = JsonSerializer.Deserialize<ControlMessage>(File.ReadAllText(ControlPath), _json)!;
                if (message.Revision != _revision)
                {
                    _revision = message.Revision; _control = message;
                    _cameraFrames = 0; _cameraStationary = 0; _largestCameraStep = 0; _lastCamera = null;
                    _jump = message.Jump; _reset = message.Reset;
                    switch (message.Action)
                    {
                        case "host": Root.Session.Host(message.Name, message.Port, message.Dedicated); break;
                        case "join": Root.Session.Join(message.Name, message.Address, message.Port); break;
                        case "rename": Root.Session.ChangeName(message.Name); break;
                        case "pause": Root.World?.SetPaused(true); break;
                        case "resume": Root.World?.SetPaused(false); break;
                        case "leave": Root.Session.Leave("Left the lobby."); break;
                        case "bad-input": Root.Session.SendInputs(new byte[] { 16, 0, 0 }); break;
                        case "quit": WriteProbe(); GetTree().Quit(); return;
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (JsonException) { }
        }
        if (_control.Action == "flood" && local is not null)
        {
            var start = local.LastServerAck;
            for (var batch = 0; batch < 8; batch++)
                Root.Session.SendInputs(InputCodec.Encode(Enumerable.Range(start + 1 + batch * 16, 16)
                    .Select(sequence => new NetworkInput(sequence, 0, -1, 0, 0, true, false, false))));
        }
        WriteProbe();
        if (ExitAfter > 0 && _elapsed > ExitAfter) GetTree().Quit();
    }

    private void WriteProbe()
    {
        if (ProbePath is null) return;
        var session = Root.Session;
        var report = new
        {
            version = GameBuild.Version, elapsed = _elapsed, revision = _revision, session.Active, session.IsServer, session.LocalName,
            cameraFrames = _cameraFrames, cameraStationary = _cameraStationary, largestCameraStep = _largestCameraStep,
            session.Status, message = Root.LastMessage,
            players = session.Players.Select(p => new
            {
                id = p.Key, p.Value.Name, p.Value.Slot, local = p.Value.Avatar.LocalControl,
                x = p.Value.Avatar.Position.X, y = p.Value.Avatar.Position.Y, z = p.Value.Avatar.Position.Z,
                ack = p.Value.Avatar.LastInput, pending = p.Value.Avatar.PendingInputs,
                correction = p.Value.Avatar.LargestCorrection
            }).ToArray()
        };
        var temporary = ProbePath + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(report, _json));
            File.Move(temporary, ProbePath, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed record ControlMessage
    {
        public int Revision { get; init; }
        public string Action { get; init; } = "";
        public string Name { get; init; } = "Player";
        public string Address { get; init; } = "127.0.0.1";
        public int Port { get; init; } = MultiplayerSession.DefaultPort;
        public bool RawCamera { get; init; }
        public bool Dedicated { get; init; }
        public int? Protocol { get; init; }
        public float X { get; init; }
        public float Y { get; init; }
        public float Yaw { get; init; }
        public float Pitch { get; init; }
        public bool Sprint { get; init; }
        public bool Jump { get; init; }
        public bool Reset { get; init; }
    }
}
