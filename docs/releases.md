# Install, update, and release

Players download **Windows Stable Setup.exe** or **Linux Stable AppImage** from
[GitHub Releases](https://github.com/ConcosHut/LuckerParty/releases/latest).
Windows installs per user without administrator permission and creates Lucker
Party shortcuts. On Linux, keep the AppImage in a writable directory, mark it
executable, and run it. Do not run either as administrator/root.

The game HUD and window title use the same exported build version as the launcher.
"Prototype 01.1" describes the movement test bed; it is not a release version.
Runs from the Godot editor show "DEVELOPMENT" instead of an installed version.

The launcher checks for updates and starts the game automatically. If checking
or downloading fails, select **Retry** or **Play installed version**. Select
**Beta** to receive development builds; select **Stable** to return to the
current stable build, even when it is older. Close the game to return to the
launcher and change channels; selecting one checks it and starts the next
session. The launcher stays alive while the
game runs; closing its window during play hides it. Another invocation defers
updates until that session exits. A launcher crash also leaves a recorded child
process identity that blocks replacement while that child is still alive.
Windows uses process creation time; Linux uses kernel start ticks and boot ID.

Windows packages are initially **unsigned**. Certificate-based signing is not
configured. Windows may show a reputation warning. Initial targets are Windows
10/11 x64 and Linux x64 with glibc and normal desktop graphics libraries. Linux
AppImage mounting requires FUSE 2; `APPIMAGE_EXTRACT_AND_RUN=1` is an alternative
on machines without it, but normal FUSE mounting is the verified update path.
The .NET runtime and complete Godot export are included; players need no SDK,
editor, GitHub login, or GitHub token. Other Linux distributions still need
compatibility testing. macOS/ARM are outside this iteration.

## Files and recovery

Windows installation: `%LOCALAPPDATA%\LuckerParty.Windows`. The stable entry
point is `Lucker Party.exe` there; `current/` is replaced as a unit on updates.
Linux replaces the AppImage itself. Do not manually move game files out of the
package. Launching the embedded game directly bypasses the launcher's session
ownership; use the shortcut/AppImage for ordinary play.

Preferences, pending launch intent, `launcher.log`, and `game.log` live in:

- Windows: `%LOCALAPPDATA%\LuckerParty\LuckerParty.Windows`
- Linux: `${XDG_DATA_HOME:-~/.local/share}/LuckerParty/LuckerParty.Linux`

Development packages use `LuckerParty.Dev.Windows` / `LuckerParty.Dev.Linux`,
with separate settings and installs. Settings and saved files outside the
installation survive updates/channel switches. Uninstalling removes the app;
remove the separate data directory explicitly only when resetting preferences
or deleting saved data is intended. There is no actual game save system yet.
A future save format migration must define downgrade behavior separately.

For offline troubleshooting, `--no-update` starts the installed game directly.
`--headless --no-update --game-smoke` runs the bounded gameplay scenario through
the launcher; `--headless --check-only` updates and reports version without
starting a game. Update restart intent is stored once and consumed by the new
launcher. Startup auto-apply in the SDK is disabled so session guards run first.

## Developer commands

Install the pinned .NET SDK, Python 3.11+, and run `python tools/bootstrap.py`
to download checksum-verified Godot 4.5.2 .NET editor/templates. Linux packaging
also needs `squashfs-tools`; Linux runtime testing needs FUSE 2. The local Arch
workspace uses an ignored extracted squashfs-tools binary under `.tools/`.
Godot/MSBuild share intermediate files: the build wrapper locks the workspace
and rejects concurrent builds/exports. Run Windows/Linux exports sequentially.

```text
python tools/dev.py check
python tools/dev.py pack --target windows --profile player --channel stable
python tools/dev.py pack --target linux --profile player --channel stable
python tools/check_distribution.py --target linux
```

Run `check_distribution.py --target windows` **on Windows**. It builds three Dev
packages and checks a real silent installation in a path with spaces, offline
failure, interrupted/corrupt HTTP delivery, old-to-new updates, Stable -> Beta
-> older Stable, exactly one game launch, and settings/saved sentinel retention.
It leaves artifacts and logs under `artifacts/update-checks/` for inspection.
Use fresh version identities when rerunning failure tests: a previously cached
valid package can legitimately avoid another download. CI has fresh machines.
The focused C# checks cover cross-process session locks, orphaned child/PID reuse,
and Stable discovery when the recent GitHub release page contains only Betas.

Local packaging defaults to Dev identities. Use `--version`, `--channel`,
`--feed`, and `--output-dir` for local testing; the retained output directory
lets Velopack build deltas. The existing [Windows SSH loop](windows-playtesting.md)
continues to use the versioned full game ZIP, independent of player releases.
Do not publish Dev packages to the production GitHub feed.

## Branches and promotion

`master` is Godot development and publishes Beta automatically on successful
pushes. `release` publishes Stable. `legacy-2` preserves the old s&box master;
`legacy`, `fixed-camera`, and `terry-races` remain intact. The original master
also has a backup tag recorded in the repository migration report.

GitHub Actions checks the gameplay, launcher guards, and real installed updates
on Ubuntu 22.04 and Windows Server 2022 before packaging both platforms. Pull
requests check/package but do not publish. Actions and editor downloads are
pinned; publishing alone has `contents: write`. The launcher locks both
platform dependency graphs. To change packages deliberately, regenerate with
`dotnet restore src/LuckerParty.Launcher/LuckerParty.Launcher.csproj -p:RestoreLockedMode=false --force-evaluate`
and review the resulting lock-file changes. Windows 10/11 desktop testing
still uses the actual PC; server/headless CI does not verify mouse feel.

`version.json` supplies the next three-part stable version. Master's package
version is `<base>-beta.<workflow-run-number>`; Release uses `<base>`. Published
versions are immutable. A second Stable push with the same version fails rather
than replacing the release. Failed publication leaves a draft for inspection;
finish that draft after verifying its artifacts, or remove it before rerunning.

To promote an accepted master revision:

1. Run checks and playtest that revision on Windows.
2. Make sure `version.json` contains a new stable version on master.
3. Fast-forward `release` to that accepted commit and push `release`.
4. Wait for both platform checks/builds and the final publish job.
5. Advance master's base to the next version for subsequent Betas.

Carry any release-only fixes back to master. For a broken Stable, publish the
known-good payload at a **higher** version rather than overwriting/deleting the
existing version. Both platforms upload into one draft; publication verifies
asset names/sizes before exposing it. Release assets include full packages,
channel/platform feeds, installers/AppImage, deltas when a previous full exists,
and `SHA256SUMS.txt`. No credentials are embedded in launcher packages.

Each new release retains the prior full package used for its delta. Keep public
historical releases available for infrequent players. Full-package fallback
remains available when a delta is unsuitable. Delta sizes depend on real asset
changes; do not infer gameplay/mesh/PCK delta savings from a metadata-only test.

Collaborators with an old checkout should fetch and make a fresh clone for the
new Godot history. To continue s&box work, check out `origin/legacy-2`. Do not
pull unrelated Godot history into an existing s&box master working tree.
