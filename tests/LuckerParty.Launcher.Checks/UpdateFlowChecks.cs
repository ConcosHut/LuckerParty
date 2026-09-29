using LuckerParty.Launcher;
using Velopack;

internal static class UpdateFlowChecks
{
    public static async Task Run(string root)
    {
        var install = Path.Combine(root, "update-policy-install");
        var data = Path.Combine(root, "update-policy-data");
        Directory.CreateDirectory(install);
        JsonFiles.Write(Path.Combine(install, "build-info.json"), new BuildInfo("1.0.0"));
        var stable = new FixtureUpdater { Update = FixtureUpdater.Release("1.1.0") };
        var beta = new FixtureUpdater { Update = FixtureUpdater.Release("1.2.0-beta.1") };
        UpdateOptions? selectedOptions = null;
        var controller = new LauncherController(new LaunchOptions(), install, data, (_, options) =>
        {
            selectedOptions = options;
            return options.ExplicitChannel!.EndsWith("-beta") ? beta : stable;
        });
        Require(await controller.RunAsync(prepareOnly: true, discoverOnly: true) == 0 && controller.UpdateAvailable &&
            controller.AvailableVersion == "1.1.0" && stable.Downloads == 0 && stable.Applies == 0 && !controller.GameRunning,
            "Startup discovers a newer version without downloading, applying or starting a game");
        Require(JsonFiles.Read<Preferences>(Path.Combine(data, "preferences.json"))!.PendingLaunch is null,
            "Discovery leaves no launch-after-update intent");
        controller.SelectChannel("beta");
        Require(!controller.UpdateAvailable, "Channel changes clear the previous channel's target immediately");
        Require(await controller.RunAsync(prepareOnly: true, discoverOnly: true) == 0 && controller.AvailableVersion == "1.2.0-beta.1" &&
            beta.Downloads == 0 && beta.Applies == 0 && selectedOptions!.AllowVersionDowngrade,
            "Channel selection discovers its target without installing and permits channel downgrades");
        beta.FailDownload = true;
        Require(await controller.RunAsync(prepareOnly: true) == 1 && controller.UpdateAvailable && beta.Applies == 0 && !controller.GameRunning,
            "Failed explicit update keeps its target available for retry without a game launch");
        beta.FailDownload = false;
        Require(await controller.RunAsync(prepareOnly: true) == 0 && beta.Downloads == 2 && beta.Applies == 1 && !controller.GameRunning,
            "Only explicit Update downloads/applies the selected build");
        var preferences = JsonFiles.Read<Preferences>(Path.Combine(data, "preferences.json"))!;
        Require(preferences.PendingLaunch is { PrepareOnly: true, CheckOnly: true } && preferences.PendingVersion == "1.2.0-beta.1" &&
            beta.RestartArguments.SequenceEqual(new[] { "--resume" }),
            "Desktop Update saves ready-only intent with the legacy Stable no-launch fallback");
        // Existing installations can still have the old launch-after-update intent.
        preferences.PendingLaunch = preferences.PendingLaunch! with { PrepareOnly = false };
        JsonFiles.Write(Path.Combine(data, "preferences.json"), preferences);
        JsonFiles.Write(Path.Combine(install, "build-info.json"), new BuildInfo("1.2.0-beta.1"));
        var resumed = new LauncherController(new LaunchOptions { Resume = true }, install, data, (_, _) => beta);
        Require(await resumed.RunAsync() == 0 && !resumed.GameRunning && !resumed.CheckOnly && resumed.Activity == LauncherActivity.Ready &&
            JsonFiles.Read<Preferences>(Path.Combine(data, "preferences.json"))!.PendingLaunch is null,
            "Updated desktop returns ready without launching, including old persisted launch intent");
        JsonFiles.Write(Path.Combine(install, "build-info.json"), new BuildInfo("1.0.0"));
        controller.SelectChannel("stable");
        stable.Update = null;
        Require(await controller.RunAsync(prepareOnly: true, discoverOnly: true) == 0 && !controller.UpdateAvailable,
            "Returning to an up-to-date channel restores Play instead of retaining a stale Update target");
        stable.Update = FixtureUpdater.Release("1.1.0");
        Require(await controller.RunAsync(discoverOnly: true) == 0 && controller.UpdateAvailable && !controller.GameRunning && stable.Downloads == 0,
            "A version discovered on Play requires an Update click instead of silently installing or launching");
        controller.SelectChannel("beta");
        beta.Update = FixtureUpdater.Release("1.0.0", downgrade: true);
        Require(await controller.RunAsync(prepareOnly: true, discoverOnly: true) == 0 && !controller.UpdateAvailable,
            "Equal-version channel transitions do not present an unnecessary Update action");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("CHECK_PASS: " + message);
    }
}
