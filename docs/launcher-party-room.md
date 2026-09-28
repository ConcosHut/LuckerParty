# Party Room launcher

The launcher uses a warm ivory, sand, coral and lilac palette, a bundled Lilita
One wordmark/display font, Nunito Sans body text, and a capsule-arena illustration.
Avalonia controls remain native, keyboard accessible and independently testable;
the illustration contains no user interface.

## Play and updates

Normal desktop startup checks and installs available updates, then waits for Play.
Changing Stable/Beta prepares that channel without starting a game. The segment
thumb animates between two always-visible choices. The installed version appears
once in the footer beside update status.

Play checks/applies updates before starting one game. The button shows progress
or Retry update when appropriate. Its chevron offers Play installed version as
the explicit offline fallback. Update restart intent distinguishes preparation
from a user-requested launch, so an update cannot turn startup preparation into
an unexpected game launch.

## Local multiplayer

Settings contains the saved, default-off Allow multiple game instances switch.
The running-instances sidebar and its layout column exist visually only while
this preference is on. Turning it off leaves existing games alive but disables
additional Play launches. With the setting on, Play starts another copy of the
installed build while games run; updates and channel changes wait for all games
to exit. Empty, running, closing and failed-close cards reflect actual processes.

Each card shows a stable instance number, duration, and Show/Close actions.
These actions use the owned process identity and its private standard-input
pipe; the game accepts only show/close and performs engine operations on its
main thread. Show restores/focuses the selected window, subject to the desktop's
focus policy. Close asks that game to quit. An explicit Force close becomes
available only after a normal close fails. Process exit, rather than the click,
removes the card and releases update ownership. PID reuse is rejected.

Closing the launcher during play minimizes it for easy taskbar restoration.
The launcher session guard and persisted identities continue protecting the
installed build until all children exit. A crashed launcher's surviving games
still block replacement; a fresh launcher waits until they close.

## Verification

`python tools/dev.py check` exercises controller ownership using three real
child processes and the rendered Avalonia home, Settings, multi-instance and
small-window layouts with the embedded fonts/art. It tests the Play dropdown,
sidebar visibility, channel deferral, and targeted Show/Close. PNGs are written
under ignored `artifacts/launcher-ui-checks/` for visual inspection.

`python tools/check_distribution.py --target linux` (or windows on Windows)
checks real installed preparation across an updater restart, subsequent single
launch, offline recovery, Stable/Beta changes and retained settings. Native
desktop focus and high-DPI feel still benefit from human playtesting.
