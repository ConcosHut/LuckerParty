# Proposed distribution and automatic updates

Research date: September 28, 2026. This is a recommendation, not an implemented
launcher or publishing pipeline. The local repository currently has `main` and
no Git remote configured.

## Recommendation

Build a small standalone C# launcher using [Velopack](https://github.com/velopack/velopack)
for installation and updates. Start with publicly downloadable GitHub Releases
for hosting. Bundle the launcher and the complete Godot export as one application
package. Keep the launcher independent of Godot and game rules.

Velopack is MIT licensed and supports Windows and Linux, .NET integration,
release channels, full update packages, and binary delta updates. It supplies
the updater; we still need to implement the launcher UI, launch sequence,
configuration, and build/publishing workflow. No paid Velopack hosting service
is required for this arrangement.

For the first version, keep the UI small: installed version, selected channel,
update status/progress, Play, and a useful error message with Retry or Play
Installed Version when a network check fails. Bundle the launcher's runtime;
players should not need Godot or a .NET SDK.

Sources: [project and license](https://github.com/velopack/velopack),
[integration](https://docs.velopack.io/integrating/overview),
[distribution](https://docs.velopack.io/distributing/overview).

## Two source branches and two player channels

| Source branch | Player channel | Publication policy |
| --- | --- | --- |
| `main` | Stable | Publish a versioned tag from a tested revision. |
| `beta` | Beta | Automatically publish successful builds after the required checks. |

Develop on `beta`, playtest, then merge the accepted changes into `main` and tag
a stable release. Carry stable fixes back into `beta`. No third long-lived
release branch is needed. Branches organize source work; channels select the
compiled versions installed on players' machines. A Git push alone does not
update anyone's installation without a successful publication.

Show only Stable and Beta to players. Internally use platform-qualified feeds:
`win-x64-stable`, `win-x64-beta`, `linux-x64-stable`, `linux-x64-beta`. Use distinct
platform package IDs/file names to prevent collisions when hosting artifacts.
Record package version, source commit, and channel in each build; replace the
current manually named prototype ZIP versions with release metadata.

Returning from beta to stable must explicitly support installing an older
version. Velopack normally only moves forward; channel changes can use its
explicit-channel and downgrade options. Keep preferences outside the managed
application files. A package downgrade cannot promise to reverse future save
format changes.

Sources: [channels](https://docs.velopack.io/packaging/channels),
[switching channels](https://docs.velopack.io/integrating/switching-channels).

## Player launch sequence

1. Run the installed Lucker Party launcher and read the selected channel.
2. Check that channel's update feed, with a bounded network timeout.
3. If a newer build exists, download it and report progress. Use Velopack's
   package validation and apply APIs rather than implementing an archive updater.
4. Apply only while the game is stopped; restart the launcher as needed.
5. Launch the game from the installed package, with its working directory set
   correctly. Keep the launcher aware of the child process so another invocation
   cannot update files underneath a running game.

An unavailable feed or interrupted download must leave the installed build
playable. Store settings and saves in a user-data location, outside replaced
application files. Validate these behaviors in our own package; do not assume
that selecting a library establishes end-to-end recovery behavior.

Velopack requires its startup hook to run first in the main executable's `Main`.
Our standalone launcher can own that entry point and be the package's main
executable, with the exported Godot game beneath it. Both update together. This
also allows a future engine port without changing the distribution mechanism.

Source: [startup and update APIs](https://docs.velopack.io/integrating/overview).

## Hosting and publishing

Use public download repositories if we want token-free installation on friends'
PCs. The source repository can remain private by publishing only binaries and
release notes to separate public repositories. CI credentials stay in CI; do
not distribute an account's private GitHub token inside the launcher.

For frequent beta publication, prefer separate stable and beta download
repositories initially. The `GithubSource` implementation inspected on the
upstream `develop` branch requests only the first ten releases before filtering
prereleases; enough beta releases in a shared repository can hide an older
stable release. Separate repositories avoid this coupling without writing a
custom source. Verify this behavior against the actual pinned SDK version when
implementing. This is a hosting detail, not a requirement for more source
branches or player channels.

GitHub Releases host the installer/AppImage, feeds, and update packages. GitHub
documents a per-asset limit below 2 GiB and no total release-size or bandwidth
limit. Its API still has rate limits; check once per launcher session, handle
failures, and avoid polling loops. These downloads are public, including beta;
private tester access would require a different access model.

Build with the existing pinned Godot/.NET toolchain and shared checks. Package
Windows as an installer and Linux as an AppImage, with the full exported game
and self-contained launcher. Velopack supports producing Windows and Linux
packages from Linux; that does not replace native Windows runtime playtesting.
Pin the CLI and SDK to matching versions rather than taking latest on every run.

Publish all platform assets into a draft, then expose the release once complete.
Use monotonic versions within each channel and retain the prior package for
delta creation. Deltas may reduce download size; measure actual Godot PCK changes
before promising savings. Recover a bad stable release by publishing its known
good content under a new higher version. Later multiplayer will also need an
explicit protocol compatibility check.

Hosting can later move to an HTTPS static server or object storage such as S3
using Velopack's web feed. Plan a transition release while the old host remains
available so installed launchers learn the new endpoint.

Sources: [update sources](https://docs.velopack.io/integrating/update-sources),
[GitHub source inspected](https://github.com/velopack/velopack/blob/develop/src/lib-csharp/Sources/GithubSource.cs),
[GitHub release limits](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases),
[cross compilation](https://docs.velopack.io/packaging/cross-compiling),
[Linux packages](https://docs.velopack.io/packaging/operating-systems/linux),
[CI example](https://docs.velopack.io/distributing/github-actions),
[delta updates](https://docs.velopack.io/packaging/deltas).

## Alternative and first implementation milestone

The fastest managed option is the itch.io desktop app plus its open-source
`butler` upload tool. Butler supports per-platform/beta channels and incremental
uploads; the itch app updates installed games. Downloading a ZIP directly from
itch does not provide automatic updates. Choose this if using the itch app as
the player-facing launcher is acceptable.

Source: [butler uploads and client updates](https://itch.io/docs/butler/pushing.html).

For our launcher, prove the integration locally before enabling publication:
package build A, install it, serve a local update feed with build B, and confirm
the launcher updates itself and starts the new game. Check offline launch,
interrupted download, invalid package, already-running game, beta-to-stable
switching, and settings preservation. Exercise an installed Windows package on
the user's PC and an AppImage on Linux. Then connect the chosen repository
destinations and automate builds and publication. The existing extracted ZIP
needs a one-time move to this launcher distribution; it cannot update itself.
