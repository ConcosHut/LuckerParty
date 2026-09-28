using System.Text.Json;
using Velopack.Sources;

namespace LuckerParty.Launcher;

internal sealed class BoundedDownloader : HttpClientFileDownloader
{
    protected override HttpClient CreateHttpClient(IDictionary<string, string>? headers, double timeout)
    {
        var client = base.CreateHttpClient(headers, timeout);
        client.Timeout = TimeSpan.FromSeconds(Math.Min(timeout > 0 ? timeout : 15, 120));
        return client;
    }
}

// SDK 1.2's GitHub source only fetches recent releases. A stable release must
// remain discoverable even after many newer beta publications.
internal class StableGithubSource : GithubSource
{
    public StableGithubSource(string repository, IFileDownloader downloader)
        : base(repository, accessToken: null, prerelease: false, downloader) { }

    protected override async Task<GithubRelease[]> GetReleases(bool includePrereleases)
    {
        var recent = await base.GetReleases(false);
        var uri = new Uri(GetApiBaseUrl(RepoUri), $"repos{RepoUri.AbsolutePath}/releases/latest");
        var json = await Downloader.DownloadString(uri.ToString(), new Dictionary<string, string>
        {
            ["Accept"] = "application/vnd.github+json"
        });
        var latest = JsonSerializer.Deserialize<GithubRelease>(json);
        if (latest is null || latest.Prerelease) throw new InvalidDataException("No stable GitHub release is available.");
        return recent.Prepend(latest).DistinctBy(release => (release.Name, release.PublishedAt)).ToArray();
    }
}
