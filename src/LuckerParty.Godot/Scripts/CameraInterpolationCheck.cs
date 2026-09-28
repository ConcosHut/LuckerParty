using Godot;

namespace LuckerParty.Godot;

// Samples the camera at 240 rendered frames/second against 60 physics ticks.
// It measures both paths in the same scene, instead of testing interpolation math
// in isolation. Can also run with a graphical display for render integration.
public partial class CameraInterpolationCheck : Node
{
    public FirstPersonPlayer Player { get; set; } = null!;

    public override async void _Ready()
    {
        try
        {
            var raw = await Sample(false);
            var smooth = await Sample(true);
            GD.Print($"CAMERA_RAW: stationary={raw.StationaryFrames}/180 largest_step={raw.LargestStep:F5}m");
            GD.Print($"CAMERA_SMOOTH: stationary={smooth.StationaryFrames}/180 largest_step={smooth.LargestStep:F5}m");
            if (raw.StationaryFrames < 100 || raw.LargestStep < 0.08f)
                throw new InvalidOperationException("Original camera did not reproduce 60 Hz stepping at 240 FPS");
            if (smooth.StationaryFrames > 10 || smooth.LargestStep > 0.035f)
                throw new InvalidOperationException("Interpolated camera still steps or freezes between physics ticks");
            GD.Print("CAMERA_CHECK_PASS: camera translation advances between physics ticks at 240 FPS");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CAMERA_CHECK_FAIL: {error.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task<(int StationaryFrames, float LargestStep)> Sample(bool interpolate)
    {
        Input.ActionRelease("move_right");
        Player.ResetToSpawn();
        Player.InterpolateCameraPosition = interpolate;
        await Frames(24);
        Input.ActionPress("move_right");
        await Frames(24); // Warm interpolation history and input at the new position.
        var previous = Player.CameraPosition.X;
        var stationary = 0;
        var largest = 0f;
        for (var frame = 0; frame < 180; frame++)
        {
            await Frames(1);
            var current = Player.CameraPosition.X;
            var step = current - previous;
            if (step < -0.0001f) throw new InvalidOperationException("Camera moves backward during uninterrupted strafing");
            if (Mathf.Abs(step) < 0.0001f) stationary++;
            largest = Mathf.Max(largest, Mathf.Abs(step));
            previous = current;
        }
        Input.ActionRelease("move_right");
        return (stationary, largest);
    }

    private async Task Frames(int count)
    {
        for (var frame = 0; frame < count; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
