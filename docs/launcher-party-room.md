# Party Room launcher

The launcher uses a warm ivory, sand, coral and lilac palette, the finalized
Clover Peak/Leafcut vector logo, Lilita One headlines, Nunito Sans body text,
and the approved four-world isometric arena illustration.
Avalonia controls remain native, keyboard accessible and independently testable;
the illustration contains no user interface.

The large stacked logo occupies the upper-left of the normal home screen.
Settings and the sidebar layout use a compact horizontal lockup. The flat logo
is deliberately kept distinct from the detailed hero, preserving recognizable
shapes at small sizes. Its exact SVG paths render through `BrandLogo` without
an installed logo font or a raster conversion. The hero uses high-quality
bitmap interpolation when scaled down.

A sand action strip groups the headline and Settings with a large bottom-right
Play split button: 440×76 normally, 400×64 in compact layouts. Narrow windows
reflow the actions into additional rows. Play has a restrained coral shadow,
shared darker edge and subtle top highlight; hover increases elevation,
pressing lowers it and disabled states remove the shadow. Existing capsule
instance cards and their controls are retained.

## Play and updates

Normal desktop startup checks and installs available updates, then waits for Play.
Changing Stable/Beta prepares that channel without starting a game. The segment
thumb animates between two always-visible choices. The installed version appears
once beneath the top-right selector beside update status. Selected channel text
stays white on coral; unselected text stays navy, including while games block
channel switching. A tooltip explains that restriction.

Play checks/applies updates before starting one game. The button shows progress
or Retry update when appropriate; a single-instance running game shows Running.
Its narrow, flush chevron offers Play installed version as
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

Cards use small native vector capsule illustrations on tinted floor tiles,
status dots and eye/close action icons. The helper Opens another game window
appears only while multiple instances are enabled and a game is running;
changing the setting refreshes that helper immediately.

## Window and controls

Custom caption controls extend the sidebar to the window edges. Windows retains
native resize borders and maximize hit testing; Linux uses border-only window
decorations. The caption Close action passes through the same live-game guard
as other close requests. The layout adapts to narrower and shorter windows.

Complete Avalonia control themes own hover, pressed, disabled and keyboard
focus states. Play remains coral on hover; the segmented selector uses a focus
underline inside its shared pill. The installed-version menu uses the same
palette. There is no need to migrate the launcher to a browser framework for
these controls. Implementation and verification are tracked in
[the polish checklist](launcher-polish-checklist.md).

Closing the launcher during play minimizes it for easy taskbar restoration.
The launcher session guard and persisted identities continue protecting the
installed build until all children exit. A crashed launcher's surviving games
still block replacement; a fresh launcher waits until they close.

## Verification

`python tools/dev.py check --suite launcher` exercises controller ownership using three real
child processes and the rendered Avalonia home, Settings, multi-instance and
small-window layouts with the embedded fonts/art. It tests the Play dropdown,
sidebar visibility, channel deferral, targeted Show/Close, actual pointer hover,
keyboard focus, split-button geometry, bottom-right action placement, narrow
sidebar layout and immediate preference/state changes.
PNGs are written
under ignored `artifacts/launcher-ui-checks/` for visual inspection.

`python tools/check_distribution.py --target linux` (or windows on Windows)
checks real installed preparation across an updater restart, subsequent single
launch, offline recovery, Stable/Beta changes and retained settings. Native
desktop focus and high-DPI feel still benefit from human playtesting.
