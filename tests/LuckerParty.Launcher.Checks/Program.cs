using System.Diagnostics;
using System.Net;
using System.Text;
using LuckerParty.Launcher;
using Velopack.Sources;

// A real, bounded child process used by the controller integration checks.
if (File.Exists(Path.Combine(AppContext.BaseDirectory, "game-fixture")))
{
    var exitFile = Path.Combine(AppContext.BaseDirectory, $"exit-{Environment.ProcessId}");
    var deadline = DateTime.UtcNow.AddSeconds(30);
    Console.WriteLine("FIXTURE_GAME_READY");
    var closeRequested = false;
    _ = Task.Run(async () =>
    {
        while (await Console.In.ReadLineAsync() is { } command)
        {
            if (command == "close" && !File.Exists(Path.Combine(AppContext.BaseDirectory, $"ignore-close-{Environment.ProcessId}"))) closeRequested = true;
            if (command == "show") File.WriteAllText(Path.Combine(AppContext.BaseDirectory, $"show-{Environment.ProcessId}"), "show");
        }
    });
    while (!closeRequested && !File.Exists(exitFile) && DateTime.UtcNow < deadline) await Task.Delay(20);
    return 0;
}

if (args.Length == 2 && args[0] == "--data-child")
{
    Require(!Directory.Exists(args[1]), "Fresh configured data directory does not exist yet");
    var controller = new LauncherController(new LaunchOptions());
    Require(controller.DataDirectory == Path.Combine(args[1], "LuckerParty", "LuckerParty.Unpackaged"),
        "Fresh Linux data directory stays outside the working/install directory");
    return 0;
}

if (args.Length == 2 && args[0] == "--lock-child")
{
    using var held = new SessionGuard(args[1]);
    using var self = Process.GetCurrentProcess();
    JsonFiles.Write(Path.Combine(args[1], "identity.json"), SessionGuard.CaptureGame(self));
    File.WriteAllText(Path.Combine(args[1], "ready"), "ready");
    while (!File.Exists(Path.Combine(args[1], "exit"))) await Task.Delay(20);
    return 0;
}

var directory = Path.Combine(Path.GetTempPath(), "lucker-launcher-check-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    if (OperatingSystem.IsLinux())
    {
        var freshData = Path.Combine(directory, "fresh-data");
        var dataStart = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        dataStart.ArgumentList.Add("--data-child");
        dataStart.ArgumentList.Add(freshData);
        dataStart.Environment["XDG_DATA_HOME"] = freshData;
        using var dataChild = Process.Start(dataStart)!;
        await dataChild.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Require(dataChild.ExitCode == 0, "Fresh Linux user data path check passes in a separate process");
    }

    var childStart = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
    childStart.ArgumentList.Add("--lock-child");
    childStart.ArgumentList.Add(directory);
    using var child = Process.Start(childStart)!;
    var deadline = DateTime.UtcNow.AddSeconds(10);
    while (!File.Exists(Path.Combine(directory, "ready")))
    {
        if (DateTime.UtcNow > deadline || child.HasExited) throw new Exception("Guard child did not start.");
        await Task.Delay(20);
    }
    try
    {
        try
        {
            using var duplicate = new SessionGuard(directory);
            throw new Exception("Two processes acquired the launcher session.");
        }
        catch (InvalidOperationException) { }
        var identity = JsonFiles.Read<GameSession>(Path.Combine(directory, "identity.json"))!;
        Require(SessionGuard.IsGameRunning(identity), "An orphaned running game still blocks updates");
        using var self = Process.GetCurrentProcess();
        var ownIdentity = SessionGuard.CaptureGame(self);
        var multiple = new Preferences { Game = identity, Games = new() { identity, ownIdentity } };
        Require(SessionGuard.LiveGames(multiple).Count == 2, "Legacy and multiple game identities are tracked without duplicates");
        Require(!SessionGuard.IsGameRunning(identity with { StartTicks = identity.StartTicks - 1 }), "PID reuse is not mistaken for an active game");
        File.WriteAllText(Path.Combine(directory, "exit"), "exit");
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Require(!SessionGuard.IsGameRunning(identity), "Exited game permits the next update");
        Require(SessionGuard.LiveGames(multiple).SequenceEqual(new[] { ownIdentity }), "Every surviving orphan still blocks updates after another child exits");
        using var recovered = new SessionGuard(directory);
        Console.WriteLine("CHECK_PASS: Cross-process guard releases after owner exit");
    }
    finally { if (!child.HasExited) child.Kill(); }

    var installation = Path.Combine(directory, "fixture-install");
    var gameDirectory = Path.Combine(installation, "game");
    Directory.CreateDirectory(gameDirectory);
    foreach (var source in Directory.EnumerateFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories))
    {
        var destination = Path.Combine(gameDirectory, Path.GetRelativePath(AppContext.BaseDirectory, source));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
    }
    File.WriteAllText(Path.Combine(gameDirectory, "game-fixture"), "fixture");
    JsonFiles.Write(Path.Combine(installation, "distribution.json"), new Distribution { GameExecutable = Path.GetFileName(Environment.ProcessPath!) });
    var multiData = Path.Combine(directory, "multiple-data");
    var controller = new LauncherController(new LaunchOptions(), installation, multiData);
    var checkedForUpdates = false;
    controller.Changed += state =>
    {
        if (state.Message.StartsWith("Checking ") || state.Message.StartsWith("Unpackaged developer")) checkedForUpdates = true;
    };
    try
    {
        Require(!controller.AllowMultipleInstances, "Multiple instances are opt-in");
        var preparation = new LauncherController(new LaunchOptions { NoUpdate = true }, installation, multiData);
        Require(await preparation.RunAsync(prepareOnly: true) == 0 && !preparation.GameRunning,
            "Startup preparation leaves Play ready without starting a game");
        checkedForUpdates = false;
        Require(await controller.RunAsync(playInstalled: true) == 0 && controller.RunningGames == 1 && !controller.Busy,
            "Desktop launch returns while its game remains running");
        Require(await controller.RunAsync() == 1 && controller.RunningGames == 1,
            "Default setting refuses a second game");
        controller.SetAllowMultipleInstances(true);
        Require(new LauncherController(new LaunchOptions(), installation, multiData).AllowMultipleInstances,
            "Multiple-instance setting survives launcher reload");
        Require(await controller.RunAsync() == 0 && controller.RunningGames == 2 && !controller.Busy,
            "Enabled setting starts a second independent game");
        Require(!checkedForUpdates, "Additional Play launches skip update checking while another game runs");
        Require(await controller.RunAsync(playInstalled: true) == 0 && controller.RunningGames == 3,
            "Play installed version also supports an additional instance");
        var firstInstance = controller.Instances.First();
        Require(await controller.ShowInstanceAsync(firstInstance.Identity), "Show targets a single owned game through its private pipe");
        await WaitFor(() => File.Exists(Path.Combine(gameDirectory, $"show-{firstInstance.Identity.Pid}")));
        Require(!await controller.ShowInstanceAsync(firstInstance.Identity with { StartTicks = firstInstance.Identity.StartTicks - 1 }),
            "Instance actions reject a mismatched process identity");
        try { controller.SelectChannel("beta"); throw new Exception("Channel changed with active games."); }
        catch (InvalidOperationException) { Console.WriteLine("CHECK_PASS: Channel changes remain deferred during multiple games"); }
        controller.SetAllowMultipleInstances(false);
        Require(await controller.RunAsync() == 1 && controller.RunningGames == 3,
            "Disabling the setting stops new launches without closing existing games");
        var identities = JsonFiles.Read<Preferences>(Path.Combine(multiData, "preferences.json"))!.Games;
        Require(identities.Count == 3 && identities.All(SessionGuard.IsGameRunning), "All running process identities are persisted");
        foreach (var game in identities.Take(2)) await controller.CloseInstanceAsync(game);
        await WaitFor(() => controller.RunningGames == 1);
        try { using var duplicate = new SessionGuard(multiData); throw new Exception("Guard released before the last game."); }
        catch (InvalidOperationException) { Console.WriteLine("CHECK_PASS: Update guard remains held until the last instance closes"); }
        File.WriteAllText(Path.Combine(gameDirectory, $"ignore-close-{identities.Last().Pid}"), "ignore");
        await controller.CloseInstanceAsync(identities.Last());
        Require(controller.RunningGames == 1 && controller.Instances.Single().CloseFailed,
            "Failed graceful close keeps the live game tracked and exposes explicit force close");
        await controller.CloseInstanceAsync(identities.Last(), force: true);
        await WaitFor(() => !controller.GameRunning);
        using var released = new SessionGuard(multiData);
        Require(JsonFiles.Read<Preferences>(Path.Combine(multiData, "preferences.json"))!.Games.Count == 0,
            "Last exit clears all identities and releases update ownership");
    }
    finally
    {
        var identities = JsonFiles.Read<Preferences>(Path.Combine(multiData, "preferences.json"))?.Games ?? new();
        foreach (var game in identities) File.WriteAllText(Path.Combine(gameDirectory, $"exit-{game.Pid}"), "exit");
        await WaitFor(() => !controller.GameRunning);
    }

    await LauncherUiChecks.Run(installation, Path.Combine(directory, "ui-data"));

    var releases = await new ExposedStableSource(new FixtureDownloader()).Read();
    Require(releases.Length == 1 && releases[0].Name == "stable-0.2.0", "Stable discovery survives a page full of beta releases");
    Console.WriteLine("LAUNCHER_CHECK_PASS: multiple instances, settings, session ownership, orphan/PID recovery, and stable discovery");
    return 0;
}
finally { Directory.Delete(directory, recursive: true); }

static void Require(bool value, string message)
{
    if (!value) throw new Exception(message);
    Console.WriteLine("CHECK_PASS: " + message);
}

static async Task WaitFor(Func<bool> condition)
{
    var deadline = DateTime.UtcNow.AddSeconds(10);
    while (!condition())
    {
        if (DateTime.UtcNow > deadline) throw new Exception("Game process lifecycle timed out.");
        await Task.Delay(20);
    }
}

sealed class ExposedStableSource(IFileDownloader downloader)
    : StableGithubSource("https://github.com/ConcosHut/LuckerParty", downloader)
{
    public Task<GithubRelease[]> Read() => GetReleases(false);
}

sealed class FixtureDownloader : HttpClientFileDownloader
{
    protected override HttpClient CreateHttpClient(IDictionary<string, string>? headers, double timeout) => new(new FixtureHandler());
}

sealed class FixtureHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var content = request.RequestUri!.AbsolutePath.EndsWith("/latest")
            ? "{\"name\":\"stable-0.2.0\",\"prerelease\":false,\"published_at\":\"2026-09-01T00:00:00Z\",\"assets\":[]}"
            : "[" + string.Join(",", Enumerable.Range(0, 20).Select(i => $"{{\"name\":\"beta-{i}\",\"prerelease\":true,\"published_at\":\"2026-09-28T00:00:00Z\",\"assets\":[]}}")) + "]";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        });
    }
}
