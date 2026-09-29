using LuckerParty.Launcher;
using Velopack;

internal sealed class FixtureUpdater : ILauncherUpdater
{
    public bool IsInstalled => true;
    public UpdateInfo? Update { get; set; }
    public bool FailDownload { get; set; }
    public bool FailCheck { get; set; }
    public int Checks { get; private set; }
    public int Downloads { get; private set; }
    public int Applies { get; private set; }
    public TaskCompletionSource? DownloadGate { get; set; }
    public int DownloadProgress { get; set; } = 100;
    public string[] RestartArguments { get; private set; } = [];
    public Task<UpdateInfo?> CheckAsync()
    {
        Checks++;
        if (FailCheck) throw new IOException("Fixture feed unavailable");
        return Task.FromResult(Update);
    }
    public async Task DownloadAsync(UpdateInfo update, Action<int> progress, CancellationToken cancellation)
    {
        Downloads++;
        if (FailDownload) throw new IOException("Fixture download interrupted");
        progress(DownloadProgress);
        if (DownloadGate is not null) await DownloadGate.Task.WaitAsync(cancellation);
        progress(100);
    }
    public void ApplyAndRestart(VelopackAsset target, string[] arguments) { Applies++; RestartArguments = arguments; }
    public static UpdateInfo Release(string version, bool downgrade = false) => new(
        new VelopackAsset { Version = SemanticVersion.Parse(version), Type = VelopackAssetType.Full }, downgrade, null, []);
}
