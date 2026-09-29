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
    public string[] RestartArguments { get; private set; } = [];
    public Task<UpdateInfo?> CheckAsync()
    {
        Checks++;
        if (FailCheck) throw new IOException("Fixture feed unavailable");
        return Task.FromResult(Update);
    }
    public Task DownloadAsync(UpdateInfo update, Action<int> progress, CancellationToken cancellation)
    {
        Downloads++;
        if (FailDownload) throw new IOException("Fixture download interrupted");
        progress(100);
        return Task.CompletedTask;
    }
    public void ApplyAndRestart(VelopackAsset target, string[] arguments) { Applies++; RestartArguments = arguments; }
    public static UpdateInfo Release(string version, bool downgrade = false) => new(
        new VelopackAsset { Version = SemanticVersion.Parse(version), Type = VelopackAssetType.Full }, downgrade, null, []);
}
