using System.Text.Json;
using System.Text.RegularExpressions;

namespace LuckerParty.Launcher;

internal sealed record LaunchOptions
{
    public bool Headless { get; init; }
    public bool NoUpdate { get; init; }
    public bool CheckOnly { get; init; }
    public bool GameSmoke { get; init; }
    public bool Resume { get; init; }
    public string? Feed { get; init; }
    public string? Channel { get; init; }

    public static LaunchOptions Parse(string[] args)
    {
        var result = new LaunchOptions();
        for (var i = 0; i < args.Length; i++)
        {
            result = args[i] switch
            {
                "--headless" => result with { Headless = true },
                "--no-update" => result with { NoUpdate = true },
                "--check-only" => result with { CheckOnly = true },
                "--game-smoke" => result with { GameSmoke = true },
                "--resume" => result with { Resume = true },
                "--feed" when i + 1 < args.Length => result with { Feed = args[++i] },
                "--channel" when i + 1 < args.Length => result with { Channel = ValidateChannel(args[++i]) },
                _ => throw new ArgumentException($"Unknown or incomplete launcher argument: {args[i]}")
            };
        }
        return result;
    }

    public static string ValidateChannel(string channel) => channel is "stable" or "beta"
        ? channel : throw new ArgumentException("Channel must be stable or beta.");
}

internal sealed record Distribution
{
    public string PackageId { get; init; } = "LuckerParty.Unpackaged";
    public string Platform { get; init; } = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
    public string Channel { get; init; } = "stable";
    public string Repository { get; init; } = "https://github.com/ConcosHut/LuckerParty";
    public string? Feed { get; init; }
    public string GameExecutable { get; init; } = OperatingSystem.IsWindows() ? "LuckerParty.exe" : "LuckerParty.x86_64";

    public static Distribution Load(string directory)
    {
        var value = JsonFiles.Read<Distribution>(Path.Combine(directory, "distribution.json")) ?? new();
        if (!Regex.IsMatch(value.PackageId, @"^[A-Za-z0-9.]+$")) throw new InvalidDataException("Invalid package ID.");
        LaunchOptions.ValidateChannel(value.Channel);
        if (Path.GetFileName(value.GameExecutable) != value.GameExecutable)
            throw new InvalidDataException("Game executable must be a file name.");
        return value;
    }
}

internal sealed record BuildInfo(string Version = "unpackaged", string Commit = "unknown", bool Dirty = false);
internal sealed record GameSession(int Pid, long StartTicks, string? BootId = null);
internal sealed record Preferences
{
    public string Channel { get; set; } = "stable";
    public bool AllowMultipleInstances { get; set; }
    public List<GameSession> Games { get; set; } = new();
    // Retained to read preferences written by the original single-game launcher.
    public GameSession? Game { get; set; }
    public LaunchOptions? PendingLaunch { get; set; }
    public string? PendingVersion { get; set; }
}

internal static class JsonFiles
{
    internal static readonly JsonSerializerOptions Format = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static T? Read<T>(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Format) : default;

    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Format));
        File.Move(temporary, path, overwrite: true);
    }
}
