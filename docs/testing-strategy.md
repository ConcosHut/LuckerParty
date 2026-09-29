# Testing strategy and harness audit

Match local verification to the behavior changed. Keep release checks, but do
not run release qualification for each visual adjustment or duplicate every
passing CI suite in the VM. Reuse evidence only when its relevant inputs remain
unchanged, and distinguish focused local results from full release results.

## What was taking time

The successful Windows job for [Beta 26](https://github.com/ConcosHut/LuckerParty/actions/runs/36513208552)
took about 7m16s, excluding the earlier failed attempt and publication:

| Stage | Observed Windows duration |
| --- | ---: |
| Pinned editor/templates | 47s |
| Full game/launcher checks | 2m12s |
| Actual installation/update checks | 2m53s |
| Player package | 48s |

These are one run's timings, not guaranteed budgets. Full Linux verification
and packaging took about 5m34s in parallel. An intermittent Godot editor import
shutdown error caused a Windows job retry, increasing the publication wait.

The preceding local process also ran Linux installation tests, the full Windows
VM suites, multiple native previews and a final published installer trial. The
VM found a real window-placement bug, but repeating all gameplay and installed
update checks for subsequent layout tweaks had diminishing value. The number
of assertions is not the main cost: game processes, three complete test packages
per OS, extra builds/imports/exports, and repeated local/CI work dominate.

## Local selection

| Change | Default local verification | Add when relevant |
| --- | --- | --- |
| Launcher layout, copy, colors, Settings UI | `dev.py check --suite launcher`; inspect affected final renders | One native Windows desktop trial for chrome, DPI, fonts, focus or OS integration |
| Launcher child control, session guard, update/restart/preferences | Launcher suite | Installed-update suite on the affected OS; native process/desktop trial for Windows behavior |
| Portable rules, input or names | `dev.py check --suite core` | Game suite when engine/network consumers are affected |
| Party rules, lobby/RPS UI or party transport | `dev.py check --suite party` | `windows_vm.py check --suite party` for native UI; game suite for shared movement/transport edits |
| Movement, camera, network, game menus/scenes | `dev.py check --suite game` | Hardware playtest for graphics, input feel or high-refresh behavior |
| Shared runtime/dependency/export/package changes | Full `dev.py check` | Actual installs on affected OSes; use CI for the other platform when sufficient |
| Documentation only | Check links/diff | No builds or player release |

All commands use `python tools/` as their prefix. `check --suite launcher`
builds its own C# dependencies and runs the existing process/control and rendered
UI checks; it does not build/import/export the game. `--suite core` similarly
runs only the portable checks. `--suite game` includes movement, camera, the
game-side launcher control protocol, core and all real multiplayer scenarios,
but skips the standalone launcher build/UI suite. Default `check` / `--suite all`
preserves full coverage. `--suite party` builds/imports the game and runs core
and multi-process party scenarios, skipping unrelated movement/launcher checks.
Add `--graphical` for the rendered host and UI captures. Normal timings run in
graphical checks; accelerated clocks are local headless automation only.
The core dependency boundary and workspace lock remain.

Initial warm local measurements were 14.6s for launcher and 1.2s for core, both
with a deliberately unavailable Godot path. Fresh restores or slower machines
can take longer. Logs are ignored under `artifacts/check-*-timing.log`.
The default full check passed in 66.8s locally, including all 45 multiplayer
scenarios. Command selection was checked to confirm that the game suite omits
only the launcher build/checks and preserves the full game/core/network commands.

Run once after integrating relevant edits. Repeat only for new relevant edits,
a failure, or unresolved uncertainty. For visual work, batch adjustments and
review final affected states instead of repeatedly reviewing every screen.
Keep semantic interaction/state tests. Exact color, wording or size assertions
should enforce an agreed requirement or previous regression; remove or relax
them when the design changes rather than preserving obsolete snapshots.

## Release and native evidence

CI still runs the full suite, actual installed updates and player packaging on
both platforms before publication. Do not weaken the all-child update guard,
failure/recovery tests, channel/version correctness, or cross-platform packaging.
They protect behavior with costly user-facing failure modes.

A Windows Server headless CI pass does not establish Windows 11 custom chrome,
DPI layout or desktop focus. A targeted VM desktop check adds that evidence; a
second full VM gameplay/updater run usually does not. For an ordinary UI change,
the published-installer desktop trial is optional once native layout and CI
installation checks pass. Use it for new installer/update wiring or a reported
player-only problem. The physical PC remains the place for real GPU/input feel.

Treat publication as a separate stage. Focused local feedback can be reported
while CI runs; do not imply publication succeeded before it does. Batch changes
into one release rather than publishing each spacing adjustment. Tool/docs-only
commits can use the existing `[skip ci]` convention when no player package is
needed; no new updater version is required for a harness selection change.

## Further improvements, not implemented in this pass

1. Cache checksum-verified Godot downloads and dependency caches in CI. This
   removes repeated downloads; restore and checksum validation still run.
2. Build/export the game once per installed-update suite, then copy that output
   and write the correct external build metadata for each test version. Keep
   all three real packages and their install/update scenarios. The game reads
   its display version from external `build-info.json`; launcher assembly/package
   versions and the test's metadata assertions must remain correct.
3. Separate ordinary Beta CI from exhaustive qualification. For launcher-only
   changes, run launcher/native-package checks; run multiplayer when game/core
   or shared build inputs change. Keep full checks for Stable and periodic/manual
   qualification, with unknown paths falling back to full coverage. This needs
   a conservative path classifier and explicit tests before changing release gates.

These are larger harness changes; measure each separately rather than bundling
them with a visual feature. This pass adds focused commands and local policy
without removing tests or changing release CI coverage.

## Windows scenario control files

The game/party test drivers hand JSON commands to local Godot processes through
atomic file replacement. A Windows reader may briefly hold the previous file,
causing `os.replace` to return WinError 5. `tools/test_control_files.py` retries
only this transient `PermissionError` on Windows for up to two seconds; persistent
permission failures still fail the check. Linux retains the direct atomic replace.
This changes test-driver reliability, not game networking or saved player files.
