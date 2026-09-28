using System.Collections.Concurrent;
using Godot;

namespace LuckerParty.Godot;

// Only enabled for games started with the launcher's private redirected stdin.
// All engine operations stay on the main thread; the reader accepts no code or paths.
public partial class LauncherControl : Node
{
    private readonly ConcurrentQueue<string> _commands = new();
    public override void _Ready()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                while (await Console.In.ReadLineAsync() is { } command)
                    if (command is "show" or "close") _commands.Enqueue(command);
            }
            catch (IOException) { }
        });
    }

    public override void _Process(double delta)
    {
        while (_commands.TryDequeue(out var command))
        {
            GD.Print($"LAUNCHER_CONTROL: {command}");
            if (command == "close") { GetTree().Quit(); return; }
            if (DisplayServer.GetName() == "headless") continue;
            var window = GetWindow();
            if (window.Mode == Window.ModeEnum.Minimized) window.Mode = Window.ModeEnum.Windowed;
            window.Show(); window.GrabFocus();
        }
    }
}
