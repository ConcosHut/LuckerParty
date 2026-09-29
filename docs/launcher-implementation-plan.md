# Velopack launcher implementation plan

Status: implemented, published, and verified on Windows and Linux. September 28,
2026. See [launcher-verification.md](launcher-verification.md) for evidence.
Operational commands are maintained in [releases.md](releases.md). This is the execution plan for the
[distribution proposal](distribution-plan.md).

September 29 follow-up: the desktop flow now separates discovery from Update
and Play. Opening the launcher/changing channels checks metadata only; an
explicit Update installs and restarts to Play without starting a game, including
old persisted launch-after-update intent. The original sequence below records
the initial implementation; [releases.md](releases.md) defines current behavior.

## Outcome and scope

Players install once, then use the Lucker Party shortcut. The launcher checks
their selected channel, updates when necessary, and starts the game. Stable is
the default; Beta is an opt-in setting. If the network fails, they can retry or
play the installed build. Settings survive updates and channel changes.

First targets: Windows x64 installer and Linux x64 AppImage. Package the entire
Godot export and the standalone C# launcher together. Use Velopack for update
mechanics and Avalonia for a small desktop UI, compatible with our .NET 8 SDK.
Neither belongs in `LuckerParty.Core` or depends on Godot. Start with our current
game, not additional gameplay, networking, or an engine migration.

Keep the configured [SSH Windows playtest loop](windows-playtesting.md). We can
test the launcher on the actual Windows desktop from `box`; CI/publishing is
not a prerequisite for local development. Do not interrupt an active playtest.

## Architecture and behavior

Add `src/LuckerParty.Launcher` with small modules for update orchestration,
channel/feed configuration, preferences, game launch, and the UI. Keep update
and launch logic separate from UI callbacks, without a new universal framework.
Add a focused launcher verification project only for substantive behavior.

Stage packages approximately as:

```text
LuckerParty.Launcher.exe (or Linux executable)
launcher assemblies and bundled runtime
distribution.json
build-info.json
game/
  complete Godot export, including executable, PCK, and runtime/data
```

Package/update the launcher and game as a unit. Publish the launcher
self-contained; no SDK or engine editor should be required on player machines.
Retain game export ZIPs for the existing SSH loop. The launcher needs its own
runtime beside the separate runtime Godot exports; avoid premature deduplication.

Default sequence: check -> download if needed -> apply/restart -> start game.
The UI shows progress while work runs; it offers Retry, Play Installed Version
after an update failure, channel selection, version, and release notes. Record
the intent to launch across an updater restart and consume it once, avoiding
restart/launch loops. Routine launches should not require an extra Play click.

Call the Velopack startup hook first in `Main`, with automatic pending-update
application explicitly disabled. Then enforce a single launcher/game session
per installation before invoking apply APIs. Keep launcher process ownership
of the game until it exits; closing the launcher window while playing hides it
rather than losing the session guard. Another invocation should report that
the game is running and defer the update. Do not invoke updater methods that
terminate an active game after a timeout. Test duplicate launch requests and
pending-update startup against a real installation.

Store preferences, logs, and pending launch intent outside replaced application
files, using per-user locations. Isolate the developer test installation from
the player installation and from the existing extracted ZIP playtest directory.
Define build identity as package version, source commit, and dirty-workspace
status; pass/log it with game launch so a process can be tied to the exact build.

## Milestone 1: launcher and repeatable packaging

1. Select and pin a compatible Velopack SDK/CLI pair and Avalonia package version.
   Add a repository-local .NET tool manifest and restore/pin package dependencies.
   Validate against the current .NET 8 SDK rather than silently upgrading the game.
2. Add the standalone launcher entry point, startup hook, minimal status window,
   fixed game path, working-directory handling, settings/log storage, and session
   guard. Allow unpackaged development launches to start the game without
   pretending they are installed Velopack applications.
3. Extend `tools/dev.py` with versioned export/package commands and explicit
   target/channel/version inputs. Replace the hard-coded prototype archive name
   with shared version metadata while preserving the existing export workflow.
   Build into fresh staging directories to exclude stale files. Commands work on
   Linux and Windows; machine-specific deployment stays optional.
4. Stage complete Windows/Linux exports and self-contained launchers. Use
   Velopack to generate Setup.exe/AppImage, feeds, and full packages. Add a simple
   repo-native icon suitable for Linux packaging and required license notices.

Done when a packaged Windows installation and Linux AppImage both start the
current game, report the intended version, and require no installed developer
SDK. Validate installation/shortcut behavior, including paths with spaces.

## Milestone 2: prove installed A-to-B updates locally

1. Use distinct development package IDs, such as `LuckerParty.Dev.Windows` and
   `LuckerParty.Dev.Linux`. Build two versioned packages A and B with observable
   launcher/build metadata changes; the payload must include the actual game.
2. Install/run A, then expose B through a local/private feed. For Windows, first
   use a feed directory copied by SCP; then verify HTTP delivery with a bounded
   private test server. AppImage validation must use a writable user directory.
3. Open A's launcher and verify check/download/apply/restart/start. Confirm the
   installed launcher and launched game's recorded identities both belong to B.
4. Exercise unavailable feed, interrupted download, corrupt package rejection,
   a running game, duplicate launcher invocation, and a downloaded pending update
   at next startup. Confirm the current installation remains usable after update
   failures. Inspect installed files, process paths, and structured logs.
5. Verify retained preferences and a saved-file sentinel outside the installation.
   Confirm the update does not reset them and uninstall behavior is documented.

Done when both platforms pass real installed update tests. Headless SDK helpers
support focused checks but do not replace the actual Windows installer and
Linux AppImage test. Capture Windows runtime evidence through SSH/task launch
and logs, and graphical evidence where available; the user checks controls.

This is the first independently useful deliverable and requires no GitHub
repository decision. Document measured build/package/transfer time.

## Milestone 3: Stable/Beta and player behavior

Use fixed package IDs per platform across both channels, e.g.
`LuckerParty.Windows` and `LuckerParty.Linux`. Developer IDs remain separate.
Internally select `win-x64-stable`, `win-x64-beta`, `linux-x64-stable`, or
`linux-x64-beta`; present only Stable/Beta in the UI. Platform artifacts must
have unique names when uploaded together.

Version examples: stable `0.2.0`; upcoming beta `0.3.0-beta.1`, then `.2`, then
stable `0.3.0`. Exact release versions will be assigned during implementation.
Package versions increase within a channel; never replace a published version
with different bytes. Preserve the same tested game revision when promoting
beta to stable, and rebuild/repackage with the correct release metadata.

Implement channel changes as explicit transitions. Beta-to-stable may install
an older version: permit downgrade for that transition, explicitly apply it,
persist the channel outside the package, and return to normal forward-only
updates afterward. Handle a requested channel that currently has no build.
Do not promise automatic reversal of future save-format migrations.

Done when Stable -> Beta -> older Stable succeeds on Windows and Linux with
preferences preserved, no cross-platform packages selected, and one game launch
per request. Verify UI responsiveness, meaningful errors, and bounded network
timeouts. Include clear installed version/channel information.

## Milestone 4: existing public repository and release pipeline

Use the existing public `ConcosHut/LuckerParty` repository for source and binary
releases, as requested. This replaces the earlier proposal for private source
and separate public download repositories under ParaLizard. Authenticate as
ParaLizard with write access for pushes and admin access for default/protected
branch renames and settings, subject to organization rules. GitHub CLI on `box` is authenticated as ParaLizard with administrator access.

Remote inspection on September 28, 2026 found default branch `master` at
`e0f6f6b8c654bb9f6fbe607569f47038ffc9ac27`, plus `legacy`, `fixed-camera`, and
`terry-races`. Leave those other branches intact. The intended layout is:

| Branch | Purpose | Player updates |
| --- | --- | --- |
| `legacy-2` | Preserved current s&box master and its history | None |
| `master` | New Godot/C# development, default branch | Beta |
| `release` | Tested versions promoted from master | Stable |

Two active branches plus an archive branch; no extra beta/main source branches
are needed. Short-lived feature branches are optional. Carry release fixes back
to master. Preserve the existing repository's issue and release history.

Perform the migration before wiring production feeds, once access is available:
fetch and recheck branch tips, save a backup tag of the original master, and push
our existing local history to a new temporary branch such as `godot-next`.
Verify that branch excludes ignored tools, keys, and artifacts. Use GitHub branch
rename operations to rename the existing master to `legacy-2`, then the new
Godot branch to `master`, and set the default to the new master. Create `release`
from an accepted Godot revision. Account for branch protection and existing
workflows; old archive branches must not publish Godot packages. Handle any
destination-name collision by inspecting it, not overwriting it.
Inspect open pull requests and branch rules before renames, and provide clone
update instructions for collaborators after the default branch changes.

Preserve both independent histories through branch names; do not force-push
Godot commits over the old master. After the remote migration, rename the local
`main` to `master` and set the correct upstream. Build the complete intended
state and record exact branch/tag tips before remote mutations. The archive
branch and backup tag provide the original s&box revision for recovery.

Both channels' releases live in this public repository, with platform-qualified
feeds and distinct asset names. Recheck the pinned SDK's GitHub-source discovery:
if it only scans a recent page, implement a focused discovery adapter/fallback
so frequent beta releases cannot hide the last stable release. This extends
feed discovery only; Velopack still handles package validation and application.
Prove that behavior before publication. Players need no GitHub credentials.

Configure:

| Trigger | Check/build | Publish |
| --- | --- | --- |
| Pull request | Required checks | None |
| Push to master | Required checks + both platform packages | Beta prerelease |
| Push/promotion to release | Required checks + both platform packages | Stable release |
| Explicit manual dispatch | Same checks, chosen development artifact | As configured |

Use a checked-in base version and monotonic automated beta numbers, such as
`0.3.0-beta.<run-number>`. The release branch's checked-in version determines the
stable version; promotion must include a new version. The successful publication
creates its version tag, attached to the tested revision. Reject duplicate
versions and advance master's base version after stable promotion. Pin CI
tools/actions and use the existing pinned
engine/templates and SDK. Run build/game/launcher checks on Linux and Windows;
retain actual package update verification in the release harness.

Use the repository's scoped Actions token for same-repository release upload;
grant write permissions only to publishing jobs. No private tokens ship with
the game. External signing credentials, if used, stay in CI secrets.
Keep concurrent publication serialized per channel, upload both platforms into
a draft, validate expected assets, and expose the release only when complete.
Do not publish broken builds or partial channel feeds.

Done when an installed Windows launcher and Linux AppImage fetch a newer build
from their configured GitHub destination without a manual ZIP download. Also
prove an older stable remains discoverable after frequent beta releases.

## Milestone 5: operations and handoff

Document installation, channel changes, offline play, release promotion,
retention, and recovery. Recover a bad stable release by publishing a known-good
payload at a higher version. Retain enough prior full packages to create deltas
and support infrequent users; measure actual Godot PCK delta size before
promising small downloads. Always retain a full-package fallback.

Test a fresh Windows install, an existing install update, and Linux portability
on our actual machines. Document minimum supported OS/runtime dependencies and
the signing status of distributed Windows installers. Initial local playtests
can be unsigned; configure release signing when an appropriate certificate is
available, without making certificate purchase a prerequisite for the prototype.
Produce a concise release runbook and an executable local update-check command.

Done when a friend can install once, opt into Beta, receive the next build, and
return to Stable, without developer tools or developer credentials.

## Implementation order and unresolved inputs

Execute milestones 1 -> 2 -> 3 -> 4 -> 5. Start with a small working launcher,
not a polished visual design. Keep commits aligned with reviewable milestones
and record actual verification results. The user authorized implementation of this plan.

The repository and public visibility are now decided. Milestone 4 needs
authenticated ParaLizard access with sufficient rights on `ConcosHut/LuckerParty`.
Dependency versions will be selected and pinned in milestone 1. Do not invent
secrets, repository permissions, or existing signing certificates.
Do not build a custom remote watcher, generic updater, user accounts, asset CDN,
multiplayer compatibility service, or additional platform targets in this work.

## Primary references

- [Velopack startup, update, and concurrency behavior](https://docs.velopack.io/integrating/overview)
- [Release channels](https://docs.velopack.io/packaging/channels)
- [Channel transitions and downgrades](https://docs.velopack.io/integrating/switching-channels)
- [Windows/Linux cross packaging](https://docs.velopack.io/packaging/cross-compiling)
- [Linux AppImage requirements](https://docs.velopack.io/packaging/operating-systems/linux)
- [Update sources and authentication](https://docs.velopack.io/integrating/update-sources)
- [Avalonia desktop support and .NET requirements](https://docs.avaloniaui.net/docs/supported-platforms)
- [Velopack CI packaging example](https://docs.velopack.io/distributing/github-actions)
- [Existing public source repository](https://github.com/ConcosHut/LuckerParty)
- [GitHub branch renames and required permissions](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-branches-in-your-repository/renaming-a-branch)
