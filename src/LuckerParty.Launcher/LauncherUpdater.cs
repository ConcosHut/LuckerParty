using Velopack;
using Velopack.Sources;

namespace LuckerParty.Launcher;

// Keep discovery separate from download/apply so checks cannot install a build.
// The small adapter also lets interaction checks observe those side effects.
internal interface ILauncherUpdater
{
    bool IsInstalled { get; }
    Task<UpdateInfo?> CheckAsync();
    Task DownloadAsync(UpdateInfo update, Action<int> progress, CancellationToken cancellation);
    void ApplyAndRestart(VelopackAsset target, string[] arguments);
}

internal sealed class VelopackLauncherUpdater(IUpdateSource source, UpdateOptions options) : ILauncherUpdater
{
    private readonly UpdateManager _manager = new(source, options);
    public bool IsInstalled => _manager.IsInstalled;
    public Task<UpdateInfo?> CheckAsync() => _manager.CheckForUpdatesAsync();
    public Task DownloadAsync(UpdateInfo update, Action<int> progress, CancellationToken cancellation)
        => _manager.DownloadUpdatesAsync(update, progress, cancellation);
    public void ApplyAndRestart(VelopackAsset target, string[] arguments)
        => _manager.ApplyUpdatesAndRestart(target, arguments);
}
