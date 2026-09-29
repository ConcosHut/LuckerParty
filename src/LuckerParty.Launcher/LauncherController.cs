using System.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace LuckerParty.Launcher;

internal enum LauncherActivity { Ready, Checking, UpdateAvailable, Downloading, Applying, Error }
internal sealed record LauncherState(string Message, int Progress = 0, bool Running = false, string Notes = "",
    LauncherActivity Activity = LauncherActivity.Ready);
internal sealed record GameInstance(GameSession Identity, int Number, DateTime StartedAt, bool Closing, bool CloseFailed);
internal sealed class OwnedGame(Process process, GameSession identity, int number)
{
    public Process Process { get; } = process;
    public GameSession Identity { get; } = identity;
    public int Number { get; } = number;
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public bool Closing { get; set; }
    public bool CloseFailed { get; set; }
}

internal sealed class LauncherController
{
    private LaunchOptions _options;
    private readonly string _directory;
    private readonly Distribution _distribution;
    private readonly string _preferencesPath;
    private readonly string _dataDirectory;
    private Preferences _preferences;
    private readonly BuildInfo _build;
    private readonly Func<IUpdateSource, UpdateOptions, ILauncherUpdater> _createUpdater;
    private readonly object _sync = new();
    private readonly object _logSync = new();
    private readonly Dictionary<int, OwnedGame> _games = new();
    private SessionGuard? _guard;
    private int _nextInstance;
    public event Action<LauncherState>? Changed;
    public bool Busy { get; private set; }
    public int RunningGames { get { lock (_sync) return _games.Count; } }
    public bool GameRunning => RunningGames > 0;
    public GameInstance[] Instances
    {
        get { lock (_sync) return _games.Values.OrderBy(game => game.Number)
            .Select(game => new GameInstance(game.Identity, game.Number, game.StartedAt, game.Closing, game.CloseFailed)).ToArray(); }
    }
    public LauncherActivity Activity { get; private set; } = LauncherActivity.Ready;
    public string UpdateStatus { get; private set; } = "Getting ready";
    public string? AvailableVersion { get; private set; }
    public bool UpdateAvailable => AvailableVersion is not null;
    public bool AllowMultipleInstances { get { lock (_sync) return _preferences.AllowMultipleInstances; } }
    public bool CheckOnly => _options.CheckOnly;
    public string Channel => _options.Channel ?? _preferences.Channel;
    public string Version => _build.Version;
    public string DataDirectory => _dataDirectory;

    public LauncherController(LaunchOptions options, string? directory = null, string? dataDirectory = null,
        Func<IUpdateSource, UpdateOptions, ILauncherUpdater>? createUpdater = null)
    {
        _directory = directory ?? AppContext.BaseDirectory;
        _distribution = Distribution.Load(_directory);
        _build = JsonFiles.Read<BuildInfo>(Path.Combine(_directory, "build-info.json")) ?? new();
        _dataDirectory = dataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify),
            "LuckerParty", _distribution.PackageId);
        _preferencesPath = Path.Combine(_dataDirectory, "preferences.json");
        _preferences = JsonFiles.Read<Preferences>(_preferencesPath) ?? new() { Channel = _distribution.Channel };
        LaunchOptions.ValidateChannel(_preferences.Channel);
        _options = options;
        _createUpdater = createUpdater ?? ((source, settings) => new VelopackLauncherUpdater(source, settings));
    }

    public void SelectChannel(string channel)
    {
        if (Busy || GameRunning) throw new InvalidOperationException("Close all game instances before changing channels.");
        _options = _options with { Channel = LaunchOptions.ValidateChannel(channel), NoUpdate = false, Resume = false };
        AvailableVersion = null;
        Activity = LauncherActivity.Ready;
        UpdateStatus = "Getting ready";
    }

    public void SetAllowMultipleInstances(bool enabled)
    {
        lock (_sync)
        {
            if (Busy) throw new InvalidOperationException("Wait for the current launch to finish.");
            // Serialize preference writes with other launchers without releasing
            // this launcher's existing guard while its games are alive.
            using var temporaryGuard = _guard is null ? new SessionGuard(_dataDirectory) : null;
            _preferences = JsonFiles.Read<Preferences>(_preferencesPath) ?? _preferences;
            _preferences.AllowMultipleInstances = enabled;
            JsonFiles.Write(_preferencesPath, _preferences);
        }
    }

    private void Report(string message, int progress = 0, bool running = false, string notes = "", LauncherActivity? activity = null)
    {
        if (activity is { } next) Activity = next;
        lock (_logSync)
        {
            Directory.CreateDirectory(_dataDirectory);
            File.AppendAllText(Path.Combine(_dataDirectory, "launcher.log"), $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        Changed?.Invoke(new(message, progress, running, notes, Activity));
    }

    public async Task<int> RunAsync(bool playInstalled = false, bool prepareOnly = false, bool discoverOnly = false)
    {
        lock (_sync)
        {
            if (Busy) return 3;
            Busy = true;
            if (!_options.Headless) _options = _options with { PrepareOnly = prepareOnly };
        }
        try
        {
            var skipUpdate = _options.NoUpdate;
            discoverOnly |= _options.DiscoverOnly;
            lock (_sync)
            {
                _guard ??= new SessionGuard(_dataDirectory);
                // Reload after acquiring ownership; another invocation may have saved state.
                _preferences = JsonFiles.Read<Preferences>(_preferencesPath) ?? _preferences;
                var liveGames = SessionGuard.LiveGames(_preferences);
                if (liveGames.Count > 0 || _games.Count > 0)
                {
                    if (_options.CheckOnly || prepareOnly || _options.PrepareOnly || !_preferences.AllowMultipleInstances
                        || liveGames.Any(game => !_games.Values.Any(owned => owned.Identity == game)))
                        throw new InvalidOperationException("The game is already running. Close all instances before updating, or enable multiple instances in their launcher to play again.");
                    skipUpdate = true;
                    playInstalled = true; // Never replace files used by any live instance.
                }
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
                    _options = _preferences.PendingLaunch with
                    {
                        Resume = false,
                        CheckOnly = _options.Headless && _preferences.PendingLaunch.CheckOnly,
                        // A desktop update always returns to Play, including
                        // older versions' persisted launch-after-update intent.
                        PrepareOnly = !_options.Headless || _preferences.PendingLaunch.PrepareOnly
                    };
                    skipUpdate = true; // Skip once, then check again on subsequent Play clicks.
                    UpdateStatus = "Up to date";
                    _preferences.PendingLaunch = null;
                    _preferences.PendingVersion = null;
                }
                _preferences.Game = null;
                _preferences.Games = liveGames;
                _preferences.Channel = Channel;
                JsonFiles.Write(_preferencesPath, _preferences);
            }

            if (!playInstalled && !skipUpdate)
            {
                var source = CreateSource();
                var transition = Channel != _distribution.Channel;
                var manager = _createUpdater(source, new UpdateOptions
                {
                    ExplicitChannel = $"{_distribution.Platform}-{Channel}",
                    AllowVersionDowngrade = transition
                });
                if (manager.IsInstalled)
                {
                    UpdateStatus = "Checking for updates";
                    Report($"Checking {Channel} updates…", activity: LauncherActivity.Checking);
                    var update = await manager.CheckAsync().WaitAsync(TimeSpan.FromSeconds(30));
                    if (update is not null && update.TargetFullRelease.Version.ToString() != _build.Version)
                    {
                        var target = update.TargetFullRelease;
                        AvailableVersion = target.Version.ToString();
                        if (discoverOnly)
                        {
                            UpdateStatus = "Update available";
                            Report($"UPDATE_AVAILABLE installed={_build.Version} target={AvailableVersion} channel={Channel}",
                                notes: target.NotesMarkdown ?? "", activity: LauncherActivity.UpdateAvailable);
                            return 0;
                        }
                        UpdateStatus = "Downloading update";
                        Report($"Downloading {target.Version}…", notes: target.NotesMarkdown ?? "", activity: LauncherActivity.Downloading);
                        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                        await manager.DownloadAsync(update, progress =>
                            Changed?.Invoke(new($"Downloading {target.Version}…", progress, Notes: target.NotesMarkdown ?? "", Activity: LauncherActivity.Downloading)), timeout.Token);
                        lock (_sync)
                        {
                            _preferences.PendingLaunch = _options with
                            {
                                Channel = Channel, Resume = false, PrepareOnly = prepareOnly || _options.PrepareOnly,
                                // Older Stable launchers know CheckOnly but not
                                // PrepareOnly. Stop them from launching on downgrade.
                                CheckOnly = !_options.Headless || _options.CheckOnly
                            };
                            _preferences.PendingVersion = target.Version.ToString();
                            JsonFiles.Write(_preferencesPath, _preferences);
                        }
                        UpdateStatus = "Applying update";
                        Report($"Applying {target.Version}; launcher will restart.", activity: LauncherActivity.Applying);
                        // Only this guarded launcher is active; no game has started yet.
                        manager.ApplyAndRestart(target, ["--resume", .. (_options.Headless ? new[] { "--headless" } : [])]);
                        return 0;
                    }
                    if (transition && update is null) throw new InvalidOperationException($"No build is available for {Channel}.");
                    AvailableVersion = null;
                    UpdateStatus = "Up to date";
                }
                else { UpdateStatus = "Developer build"; Report("Unpackaged developer launch; installation updates are unavailable."); }
            }
            else if (!GameRunning && !UpdateAvailable && (playInstalled || _options.NoUpdate)) UpdateStatus = "Installed version";
            if (_options.CheckOnly || prepareOnly || _options.PrepareOnly)
            {
                Report($"LAUNCHER_READY version={_build.Version} channel={_distribution.Channel}", activity: LauncherActivity.Ready);
                return 0;
            }
            Activity = LauncherActivity.Ready;
            return await StartGameAsync();
        }
        catch (Exception error)
        {
            UpdateStatus = "Update or launch needs attention";
            Report($"LAUNCHER_ERROR: {error.Message}", activity: LauncherActivity.Error);
            return 1;
        }
        finally
        {
            lock (_sync) { Busy = false; ReleaseIdleGuard(); }
        }
    }

    private void ReleaseIdleGuard()
    {
        if (Busy || _games.Count > 0) return;
        _guard?.Dispose(); _guard = null;
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
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true
        };
        if (_options.GameSmoke)
        {
            foreach (var argument in new[] { "--headless", "--fixed-fps", "60", "--", "--smoke-test" })
                start.ArgumentList.Add(argument);
        }
        else start.ArgumentList.Add("--");
        start.ArgumentList.Add("--launcher-control");
        var game = Process.Start(start) ?? throw new InvalidOperationException("Could not start the game.");
        try
        {
            var identity = SessionGuard.CaptureGame(game);
            lock (_sync)
            {
                _preferences.Games.Add(identity);
                JsonFiles.Write(_preferencesPath, _preferences);
                _games.Add(game.Id, new OwnedGame(game, identity, ++_nextInstance));
            }
        }
        catch
        {
            // Do not leave an unrecorded child that a later launcher could
            // replace. This process belongs to this launch attempt only.
            if (!game.HasExited) game.Kill(entireProcessTree: true);
            await game.WaitForExitAsync();
            lock (_sync) _preferences.Games.RemoveAll(session => session.Pid == game.Id);
            game.Dispose();
            throw;
        }
        var completion = TrackGameAsync(game);
        Report($"GAME_STARTED version={_build.Version} channel={_distribution.Channel} commit={_build.Commit} pid={game.Id}", running: GameRunning);
        // The desktop UI can accept another Play click while this child runs.
        // Headless invocations keep their original exit-code/smoke-test contract.
        return _options.Headless ? await completion : 0;
    }

    private async Task<int> TrackGameAsync(Process game)
    {
        var smokePassed = false;
        async Task ReadLogAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                if (line.Contains("SMOKE_TEST_PASS:")) smokePassed = true;
                lock (_logSync)
                    File.AppendAllText(Path.Combine(_dataDirectory, "game.log"), $"{DateTime.UtcNow:O} build={_build.Version} pid={game.Id} {line}{Environment.NewLine}");
            }
        }
        try
        {
            var stdout = ReadLogAsync(game.StandardOutput);
            var stderr = ReadLogAsync(game.StandardError);
            await game.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
            if (_options.GameSmoke && !smokePassed)
                throw new InvalidOperationException("The game smoke scenario did not pass; inspect game.log.");
            return game.ExitCode;
        }
        catch (Exception error)
        {
            Report($"LAUNCHER_ERROR: {error.Message}");
            return 1;
        }
        finally
        {
            // Keep the shared guard until every game has actually exited.
            if (game.HasExited)
            {
                lock (_sync)
                {
                    _games.Remove(game.Id, out var owned);
                    _preferences.Games.RemoveAll(session => session == owned?.Identity);
                    try { JsonFiles.Write(_preferencesPath, _preferences); }
                    finally { ReleaseIdleGuard(); }
                }
                Report($"GAME_EXITED version={_build.Version} code={game.ExitCode}", running: GameRunning);
                game.Dispose();
            }
        }
    }

    public Task<bool> ShowInstanceAsync(GameSession identity) => SendCommandAsync(identity, "show");

    private async Task<bool> SendCommandAsync(GameSession identity, string command)
    {
        OwnedGame? owned;
        lock (_sync) _games.TryGetValue(identity.Pid, out owned);
        if (owned is null || owned.Identity != identity || !SessionGuard.IsGameRunning(identity)) return false;
        try
        {
            await owned.Process.StandardInput.WriteLineAsync(command);
            await owned.Process.StandardInput.FlushAsync();
            return true;
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException)
        { return false; }
    }

    public async Task CloseInstanceAsync(GameSession identity, bool force = false)
    {
        OwnedGame? owned;
        lock (_sync)
        {
            if (!_games.TryGetValue(identity.Pid, out owned) || owned.Identity != identity || owned.Closing) return;
            owned.Closing = true; owned.CloseFailed = false;
        }
        Report($"INSTANCE_CLOSING: number={owned.Number}", running: GameRunning);
        try
        {
            if (!SessionGuard.IsGameRunning(identity)) return;
            if (force) owned.Process.Kill(entireProcessTree: true);
            else if (!await SendCommandAsync(identity, "close")) throw new IOException("The game did not receive the close request.");
            await owned.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
        }
        catch (Exception error) when (error is TimeoutException or IOException or InvalidOperationException)
        {
            lock (_sync) { owned.Closing = false; owned.CloseFailed = true; }
            Report("This game did not close. Retry, or use Force close on its card.", running: GameRunning);
        }
    }
}
