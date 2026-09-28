using System.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace LuckerParty.Launcher;

internal sealed record LauncherState(string Message, int Progress = 0, bool Running = false, string Notes = "");

internal sealed class LauncherController
{
    private LaunchOptions _options;
    private readonly string _directory = AppContext.BaseDirectory;
    private readonly Distribution _distribution;
    private readonly string _preferencesPath;
    private readonly string _dataDirectory;
    private Preferences _preferences;
    private readonly BuildInfo _build;
    public event Action<LauncherState>? Changed;
    public bool Busy { get; private set; }
    public bool GameRunning { get; private set; }
    public bool CheckOnly => _options.CheckOnly;
    public string Channel => _options.Channel ?? _preferences.Channel;
    public string Version => _build.Version;
    public string DataDirectory => _dataDirectory;

    public LauncherController(LaunchOptions options)
    {
        _distribution = Distribution.Load(_directory);
        _build = JsonFiles.Read<BuildInfo>(Path.Combine(_directory, "build-info.json")) ?? new();
        _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LuckerParty", _distribution.PackageId);
        _preferencesPath = Path.Combine(_dataDirectory, "preferences.json");
        _preferences = JsonFiles.Read<Preferences>(_preferencesPath) ?? new() { Channel = _distribution.Channel };
        LaunchOptions.ValidateChannel(_preferences.Channel);
        _options = options;
    }

    public void SelectChannel(string channel)
    {
        if (Busy) throw new InvalidOperationException("Wait for the current launch to finish.");
        _options = _options with { Channel = LaunchOptions.ValidateChannel(channel), NoUpdate = false, Resume = false };
    }

    private void Report(string message, int progress = 0, bool running = false, string notes = "")
    {
        Directory.CreateDirectory(_dataDirectory);
        File.AppendAllText(Path.Combine(_dataDirectory, "launcher.log"), $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        Changed?.Invoke(new(message, progress, running, notes));
    }

    public async Task<int> RunAsync(bool playInstalled = false)
    {
        if (Busy) return 3;
        Busy = true;
        try
        {
            using var guard = new SessionGuard(_dataDirectory);
            // Reload after acquiring ownership; another invocation may have saved state.
            _preferences = JsonFiles.Read<Preferences>(_preferencesPath) ?? _preferences;
            if (SessionGuard.IsGameRunning(_preferences.Game))
                throw new InvalidOperationException("The game is already running. Update deferred until it closes.");
            var skipUpdate = _options.NoUpdate;
            if (playInstalled)
            {
                _options = _options with { Resume = false };
                _preferences.PendingLaunch = null;
                _preferences.PendingVersion = null;
            }
            if (_options.Resume)
            {
                if (_preferences.PendingLaunch is null || _preferences.PendingVersion != _build.Version)
                    throw new InvalidOperationException("Update restart did not reach the requested version. Retry or play the installed build.");
                _options = _preferences.PendingLaunch with { Resume = false };
                skipUpdate = true; // Skip once, then check again on subsequent Play clicks.
                _preferences.PendingLaunch = null;
                _preferences.PendingVersion = null;
            }
            _preferences.Game = null;
            _preferences.Channel = Channel;
            JsonFiles.Write(_preferencesPath, _preferences);

            if (!playInstalled && !skipUpdate)
            {
                var source = CreateSource();
                var transition = Channel != _distribution.Channel;
                var manager = new UpdateManager(source, new UpdateOptions
                {
                    ExplicitChannel = $"{_distribution.Platform}-{Channel}",
                    AllowVersionDowngrade = transition
                });
                if (manager.IsInstalled)
                {
                    Report($"Checking {Channel} updates…");
                    var update = await manager.CheckForUpdatesAsync().WaitAsync(TimeSpan.FromSeconds(30));
                    if (update is not null)
                    {
                        var target = update.TargetFullRelease;
                        Report($"Downloading {target.Version}…", notes: target.NotesMarkdown ?? "");
                        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                        await manager.DownloadUpdatesAsync(update, progress =>
                            Changed?.Invoke(new($"Downloading {target.Version}…", progress, Notes: target.NotesMarkdown ?? "")), timeout.Token);
                        _preferences.PendingLaunch = _options with { Channel = Channel, Resume = false };
                        _preferences.PendingVersion = target.Version.ToString();
                        JsonFiles.Write(_preferencesPath, _preferences);
                        Report($"Applying {target.Version}; launcher will restart.");
                        // Only this guarded launcher is active; no game has started yet.
                        manager.ApplyUpdatesAndRestart(target, ["--resume", .. (_options.Headless ? new[] { "--headless" } : [])]);
                        return 0;
                    }
                    if (transition) throw new InvalidOperationException($"No build is available for {Channel}.");
                }
                else Report("Unpackaged developer launch; installation updates are unavailable.");
            }
            if (_options.CheckOnly)
            {
                Report($"LAUNCHER_READY version={_build.Version} channel={_distribution.Channel}");
                return 0;
            }
            return await StartGameAsync();
        }
        catch (Exception error)
        {
            Report($"LAUNCHER_ERROR: {error.Message}");
            return 1;
        }
        finally { Busy = false; }
    }

    private IUpdateSource CreateSource()
    {
        var feed = _options.Feed ?? _distribution.Feed;
        var downloader = new BoundedDownloader();
        if (feed is not null)
            return Uri.TryCreate(feed, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                ? new SimpleWebSource(uri, downloader)
                : new SimpleFileSource(new DirectoryInfo(feed));
        return Channel == "stable" ? new StableGithubSource(_distribution.Repository, downloader)
            : new GithubSource(_distribution.Repository, accessToken: null, prerelease: true, downloader);
    }

    private async Task<int> StartGameAsync()
    {
        var gameDirectory = Path.Combine(_directory, "game");
        var executable = Path.Combine(gameDirectory, _distribution.GameExecutable);
        if (!File.Exists(executable)) throw new FileNotFoundException("The game export is missing. Reinstall the complete package.", executable);
        var gameBuild = JsonFiles.Read<BuildInfo>(Path.Combine(gameDirectory, "build-info.json"));
        if (_build.Version != "unpackaged" && (gameBuild?.Version != _build.Version || gameBuild.Commit != _build.Commit))
            throw new InvalidDataException("Launcher and game build identities differ. Reinstall the complete package.");
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = gameDirectory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (_options.GameSmoke)
        {
            foreach (var argument in new[] { "--headless", "--fixed-fps", "60", "--", "--smoke-test" })
                start.ArgumentList.Add(argument);
        }
        using var game = Process.Start(start) ?? throw new InvalidOperationException("Could not start the game.");
        GameRunning = true;
        try
        {
            _preferences.Game = SessionGuard.CaptureGame(game);
            JsonFiles.Write(_preferencesPath, _preferences);
        }
        catch
        {
            // Do not leave an unrecorded child that a later launcher could
            // replace. This process belongs to this launch attempt only.
            if (!game.HasExited) game.Kill(entireProcessTree: true);
            GameRunning = false;
            throw;
        }
        Report($"GAME_STARTED version={_build.Version} channel={_distribution.Channel} commit={_build.Commit} pid={game.Id}", running: true);
        var smokePassed = false;
        var logGate = new SemaphoreSlim(1, 1);
        async Task ReadLogAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                if (line.Contains("SMOKE_TEST_PASS:")) smokePassed = true;
                await logGate.WaitAsync();
                try { await File.AppendAllTextAsync(Path.Combine(_dataDirectory, "game.log"), $"{DateTime.UtcNow:O} build={_build.Version} {line}{Environment.NewLine}"); }
                finally { logGate.Release(); }
            }
        }
        var stdout = ReadLogAsync(game.StandardOutput);
        var stderr = ReadLogAsync(game.StandardError);
        await game.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        logGate.Dispose();
        _preferences.Game = null;
        JsonFiles.Write(_preferencesPath, _preferences);
        GameRunning = false;
        if (_options.GameSmoke && !smokePassed)
            throw new InvalidOperationException("The game smoke scenario did not pass; inspect game.log.");
        Report($"GAME_EXITED version={_build.Version} code={game.ExitCode}");
        return game.ExitCode;
    }
}
