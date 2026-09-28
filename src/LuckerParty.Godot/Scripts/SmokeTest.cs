using Godot;

namespace LuckerParty.Godot;

// Runs the actual character controller against the actual test-bed colliders.
// Launch with -- --smoke-test. An assertion failure exits with code 1.
public partial class SmokeTest : Node
{
    public FirstPersonPlayer Player { get; set; } = null!;

    public override async void _Ready()
    {
        try
        {
            await Frames(30);
            Require(Player.IsOnFloor(), "Character settles on the floor");
            Require(Mathf.Abs(Player.Position.Y) < 0.02f, "Capsule feet rest at floor height");

            var start = Player.Position;
            Input.ActionPress("move_forward");
            await Frames(60);
            Input.ActionRelease("move_forward");
            var straightDistance = start.DistanceTo(Player.Position);
            Require(Mathf.Abs(straightDistance - 6) < 0.2f, "Forward movement covers six meters per second");

            Player.ResetToSpawn();
            await Frames(3);
            start = Player.Position;
            Input.ActionPress("move_forward");
            Input.ActionPress("move_left");
            await Frames(60);
            Input.ActionRelease("move_forward");
            Input.ActionRelease("move_left");
            Require(Mathf.Abs(start.DistanceTo(Player.Position) - straightDistance) < 0.2f,
                "Diagonal input does not increase movement speed");

            Player.ResetToSpawn();
            await Frames(3);
            Input.ActionPress("move_forward");
            Input.ActionPress("sprint");
            await Frames(30);
            Input.ActionRelease("move_forward");
            Input.ActionRelease("sprint");
            Require(Mathf.Abs(Player.Position.Z - 1.5f) < 0.2f, "Sprint moves at nine meters per second");

            Player.ResetToSpawn();
            await Frames(3);
            Input.ActionPress("move_forward");
            await Frames(180);
            Input.ActionRelease("move_forward");
            Require(Player.Position.Z > -3.7f && Player.Position.Z < -3.5f,
                "Solid blue box blocks the capsule");

            Player.ResetToSpawn();
            await Frames(3);
            Input.ActionPress("jump");
            await Frames(10);
            Input.ActionRelease("jump");
            Require(Player.Position.Y > 0.5f, "Jump lifts the character off the floor");
            await Frames(60);
            Require(Player.IsOnFloor(), "Character lands after jumping");

            Player.ControlsEnabled = false;
            start = Player.Position;
            Input.ActionPress("move_forward");
            await Frames(20);
            Input.ActionRelease("move_forward");
            Require(start.DistanceTo(Player.Position) < 0.02f, "Disabled controls prevent walking");
            Player.ControlsEnabled = true;

            // Headless displays cannot capture a hardware mouse. Exercise the same
            // look operation used by the captured-mouse event handler.
            Player.ApplyMouseLook(new Vector2(120, -100000));
            Require(Player.Rotation.Y < -0.2f, "Mouse motion turns the character");
            Require(Mathf.Abs(Player.GetNode<Node3D>("Head").Rotation.X - Mathf.DegToRad(85)) < 0.01f,
                "Vertical look is clamped");

            Player.ResetToSpawn();
            Require(Player.Position.IsEqualApprox(Player.SpawnPosition)
                && Player.Velocity.IsEqualApprox(Vector3.Zero)
                && Player.Rotation.IsEqualApprox(Vector3.Zero)
                && Player.GetNode<Node3D>("Head").Rotation.IsEqualApprox(Vector3.Zero),
                "Reset restores position, velocity, and camera orientation");

            // Exercise the bound key event, rather than only calling the reset method.
            Player.Position = new Vector3(8, 0.05f, 8);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.R, Pressed = true });
            await Frames(2);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.R, Pressed = false });
            Require(Player.Position.DistanceTo(Player.SpawnPosition) < 0.1f, "R key resets the character");

            GD.Print("SMOKE_TEST_PASS: 13 movement, collision, camera, and reset checks");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"SMOKE_TEST_FAIL: {error.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print($"CHECK_PASS: {message}");
    }
}
