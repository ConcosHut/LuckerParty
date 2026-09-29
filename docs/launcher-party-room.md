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

A vertical ivory-to-sand gradient grounds the arena and the large bottom-right
Play button: 480×88 normally, 440×76 in compact layouts, constrained by available
width. The hero uses Multiply blending at render time to integrate its white
backdrop; the approved PNG is unchanged. The old tagline is removed.
Play has a restrained coral shadow,
shared darker edge and subtle top highlight; hover increases elevation,
pressing lowers it and disabled states remove the shadow. Existing capsule
instance cards and their controls are retained.

Empty helper text collapses completely, and the action strip uses 16px above
and 12px below its controls. Settings is an icon-only cog in the window's global
top-left caption, with a tooltip and accessible name. It anchors to the same
position on the home and Settings pages, including compact/sidebar layouts. Its open state
has a warm, darker face, stronger edge and inset shadow to read as pressed in.
Clicking it again, Space or Escape returns home; there is no Back button.
The green check and Up to date text form one closely spaced, centered pair
aligned to the right beneath the channel selector.

## Play and updates

Normal desktop startup only discovers available updates. Changing Stable/Beta
only discovers that channel's version, without downloading or installing. The segment
thumb animates between two always-visible choices. The installed version appears
once beneath the top-right selector beside update status. Selected channel text
stays white on coral; unselected text stays navy, including while games block
channel switching. A tooltip explains that restriction.

When the selected channel has a different version, Play becomes a green Update
button with a download icon and target-version helper. Clicking Update installs
that version and restarts the launcher to Play; it never starts the game. This
also applies to an older launcher's saved launch-after-update intent. Play checks
again before starting one game; a newly discovered mismatch requires a separate
Update click. The button shows progress or Retry update when appropriate;
a single-instance running game shows Running.
Play occupies the whole button without a chevron. Update and Retry update expose
a flush square chevron offering Play installed version as the offline fallback.
Busy actions hide that chevron. Download progress fills the disabled main button
behind Updating, its actual percentage and a secondary status line. Checking,
installation and starting show their stage text without a transfer percentage;
no separate progress bar remains below the button. Headless automation retains its explicit
update-and-prepare or update-and-launch commands; `--discover-only` exercises
the metadata-only path without either side effect. A small updater adapter keeps
check/download/apply boundaries independently observable in interaction tests.

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
sidebar layout and immediate preference/state changes. Update-policy checks cover
startup/channel discovery without downloading, explicit Update, retry, legacy
restart intent, the green hover state and deferral while games use the install.
An asynchronous fixture holds a download at 62% to verify its disabled action,
embedded fill, secondary status and proportional progress after resizing.
PNGs are written
under ignored `artifacts/launcher-ui-checks/` for visual inspection.

`python tools/check_distribution.py --target linux` (or windows on Windows)
checks metadata-only discovery with zero package requests, real installed
preparation across an updater restart, subsequent single
launch, offline recovery, Stable/Beta changes and retained settings. Native
desktop focus and high-DPI feel still benefit from human playtesting.
