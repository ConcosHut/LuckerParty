using System.Diagnostics;
using System.Net;
using System.Text;
using LuckerParty.Launcher;
using Velopack.Sources;

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
        Require(!SessionGuard.IsGameRunning(identity with { StartTicks = identity.StartTicks - 1 }), "PID reuse is not mistaken for an active game");
        File.WriteAllText(Path.Combine(directory, "exit"), "exit");
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Require(!SessionGuard.IsGameRunning(identity), "Exited game permits the next update");
        using var recovered = new SessionGuard(directory);
        Console.WriteLine("CHECK_PASS: Cross-process guard releases after owner exit");
    }
    finally { if (!child.HasExited) child.Kill(); }

    var releases = await new ExposedStableSource(new FixtureDownloader()).Read();
    Require(releases.Length == 1 && releases[0].Name == "stable-0.2.0", "Stable discovery survives a page full of beta releases");
    Console.WriteLine("LAUNCHER_CHECK_PASS: session ownership, orphan/PID recovery, and stable discovery");
    return 0;
}
finally { Directory.Delete(directory, recursive: true); }

static void Require(bool value, string message)
{
    if (!value) throw new Exception(message);
    Console.WriteLine("CHECK_PASS: " + message);
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
