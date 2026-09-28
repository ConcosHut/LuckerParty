using Godot;
using System.Text.Json;

namespace LuckerParty.Godot;

public static class GameBuild
{
    public static string Version
    {
        get
        {
            if (OS.HasFeature("editor")) return "DEVELOPMENT";
            var path = Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!, "build-info.json");
            if (!File.Exists(path)) return "UNVERSIONED BUILD";
            using var metadata = JsonDocument.Parse(File.ReadAllText(path));
            return metadata.RootElement.GetProperty("version").GetString()!;
        }
    }
}
