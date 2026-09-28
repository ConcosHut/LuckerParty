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

## Fast Linux-to-Windows development loop

The updater also works with a private HTTP feed or local directory, so it can
deliver frequent development builds. It does not by itself arrange remote
execution or continuously watch for a build while the launcher is closed.
Testing updater logic is also distinct from running and debugging the game.

For the user's rapid playtesting workflow, add an opt-in Windows desktop helper
alongside the distribution launcher. Start it once in the logged-in user session
and enable automatic playtesting for that session. It watches a private feed
reachable from Windows, through the LAN or an existing private network. The
same Velopack packaging/update code should handle the package; the helper adds
build notification, launch requests, and launch status.

The proposed sequence is:

1. Finish a change on Linux and run the appropriate checks.
2. Export the Windows game and package it with the launcher. Assign a unique
   increasing development version and record the source revision, including
   whether the workspace has uncommitted changes.
3. Publish the completed package to the private development feed, then publish
   a small ready/launch request naming that exact build. Never announce a partial
   package, and use a unique request ID so polling cannot repeatedly launch it.
4. The Windows helper notices the request, downloads/applies the package with
   the launcher, and starts the requested version on the desktop. Keep the
   helper separate from the application files replaced during an update.
5. Report the installed build and launch result. Later, return logs and bounded
   smoke-check results to the Linux workflow as useful debugging evidence.

No commit, push, or GitHub Actions run is necessary for each local iteration.
The existing Linux-to-Windows export remains the build path; Windows receives
a runnable package rather than requiring a local SDK or engine editor. There
will still be build, packaging, and transfer time; measure those before promising
a particular turnaround time. A launched process is not proof of correct
rendering or controls.

The private development feed is separate from the two player channels and does
not require another long-lived source branch. Keep its installation and user
data separate from the regular player installation. Beta remains useful for
sharing checkpoints with friends and testing the real distribution pipeline.

Only the helper's armed development mode responds to remote launch requests.
Use an authenticated or access-controlled feed and fixed package/launch actions,
rather than accepting arbitrary shell commands. If the game is already running,
queue the new build and let the user close it; do not interrupt a playtest.
Require the Windows PC to be awake, the user logged in, and the helper connected.
The current Linux session has no configured Windows helper connection.

A normal app in the user's desktop session is the simplest starting point.
Windows services cannot directly interact with the desktop; installing a
background service alone would not solve visible game launch. Start with a
small helper, then consider folding the watch mode into the launcher once the
workflow works. Full remote debugging or source editing on Windows would be a
separate capability.

Sources: [local and HTTP update feeds](https://docs.velopack.io/integrating/update-sources),
[development testing support](https://docs.velopack.io/integrating/testing),
[Windows services and interactive sessions](https://learn.microsoft.com/en-us/windows/win32/services/interactive-services).

For our launcher, prove the integration locally before enabling publication:
package build A, install it, serve a local update feed with build B, and confirm
the launcher updates itself and starts the new game. Check offline launch,
interrupted download, invalid package, already-running game, beta-to-stable
switching, and settings preservation. Exercise an installed Windows package on
the user's PC and an AppImage on Linux. Then connect the chosen repository
destinations and automate builds and publication. The existing extracted ZIP
needs a one-time move to this launcher distribution; it cannot update itself.
