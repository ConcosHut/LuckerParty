# Launcher verification — September 28, 2026

Local tests used Velopack 1.2.158, Avalonia 11.3.22, .NET SDK 8.0.425/runtime
8.0.31, and Godot 4.5.2 .NET. The launcher and game were packaged together with
bundled runtimes. The first export conflict from concurrent platform commands
was rejected by the error checks; a workspace lock now prevents those races.

## Actual installations

Linux: writable AppImage in a path containing spaces on `box`. Installed
0.2.0 -> 0.2.1, then 0.3.0-beta.1 -> 0.2.1. The automated HTTP harness also ran
fresh 0.0.1 -> 0.0.2 -> 0.1.0-beta.1 -> 0.0.2 and reported
`INSTALLED_UPDATE_CHECK_PASS`.

Windows: actual PC at the configured SSH target. Setup.exe installed per user
without elevation, produced its stable shortcut stub and complete `current/`
files. Installed 0.2.0 -> 0.2.1 -> 0.3.0-beta.1 -> 0.2.1. The HTTP harness then
installed into `F:\dev\lucker-party-playtest\launcher installed with spaces` and
ran 0.2.0 -> 0.2.2 -> 0.3.0-beta.1 -> 0.2.2. It reported
`INSTALLED_UPDATE_CHECK_PASS`.

Both HTTP harnesses rejected unavailable servers, truncated responses, and
corrupt packages; the unchanged installed game still passed its smoke scenario
after these failures. Each successful update restarted the launcher and ran
exactly one game smoke scenario from the requested package version. The saved
sentinel and selected channel survived updates/downgrades, and pending intent
was consumed. Tests use Dev package IDs, separate from the existing extracted
ZIP and future player installs. Local logs remain in ignored `artifacts/`.

The Windows shortcut stub exits before its child and does not propagate failure
codes. The bounded harness therefore checks `current/LuckerParty.Launcher.exe`;
ordinary shortcut launch and update were verified separately. UTF-8 launcher
output/logs avoid Windows test-capture encoding errors.

Focused checks passed for cross-process lock ownership/release, orphaned child
recovery, PID reuse, and an old Stable release hidden behind recent Betas. A
Windows installed launcher with a live recorded process deferred its update
before feed discovery. SDK startup auto-apply is explicitly disabled.

Gameplay checks passed: all 15 movement/collision/camera/reset assertions and
240 FPS camera interpolation. Raw camera translation stopped on 135/180 sampled
frames; interpolated translation stopped on 0/180. User previously verified the
movement improvement on their 240 Hz Windows display.

## Size and timing

Metadata-only 0.2.0 -> 0.2.1 packages:

| Platform | Full package | Delta |
| --- | --- | --- |
| Windows x64 | 108.62 MiB | 0.16 MiB |
| Linux x64 | 69.36 MiB | 0.15 MiB |

Warm launcher packaging took about 10–12 seconds on Windows cross-packaging and
4 seconds for Linux, after the Godot export and launcher publication. A local
Windows HTTP full-package update plus restart/smoke took about 3 seconds; actual
internet transfer times depend on the connection. These changes barely touched
assets; they do not establish delta sizes for future Godot PCK/game content.

## Remaining human/platform checks

Headless update/game checks do not verify input feel or every Linux desktop.
Windows signing is not configured. The Windows interactive task rendered the launcher in desktop session 1,
verified its error/retry/offline buttons, closed it cleanly, and rendered the
packaged game to a viewport image. Linux created its X11 launcher window, but
the desktop was locked, so its visual layout was not verified there.
Production GitHub feed checks and final CI publication results follow below.

An additional installed startup check exposed that Linux .NET process start
timestamps can differ across callers. The first unit check compared in one
process and missed this. Linux session identity now uses `/proc/<pid>/stat`
start ticks plus kernel boot ID; Windows keeps its exact process creation time.
The focused check now reads the child process's persisted identity, proving
cross-process recovery instead of comparing two timestamps from one caller.
Old Linux session records without boot IDs conservatively block replacement
while their PID remains alive.
