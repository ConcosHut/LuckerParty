# Party Room polish checklist

Scope: approved Party Room mockups, the screenshot review, and the seven user
additions. Keep Avalonia and the existing C# updater/process controller. Work
in parallel on separate files, then verify the combined result before release.

The first polish pass shipped in Beta 25. The following iteration improves
space usage and Settings navigation, with native verification in the Windows VM.

## Explicit Update interaction — September 29, 2026

- [x] Opening, channel selection and last-game exit discover metadata without downloading/applying.
- [x] Present a green Update action and target version on a mismatch; keep current-version Play coral.
- [x] Install only on an explicit Update click and return the restarted desktop to Play.
- [x] Convert older saved launch-after-update intent to ready-only behavior.
- [x] Use a check-only compatibility flag to prevent the old Stable launcher from starting a game on downgrade.
- [x] Keep all-child/session guards, offline installed-version Play and retry behavior.
- [x] Ignore queued controller events after a launcher window closes.
- [x] Verify actual UI clicks, Update hover, retry, channel changes, last-game deferral and explicit install.
- [x] Verify real Linux metadata-only discovery without package requests, HTTP update failures/recovery,
      upgrade/downgrade and retained settings. New headless discovery uses the same controller path.
- [ ] Publish after the Windows and Linux CI release gate passes.

Local evidence: ignored `artifacts/explicit-update-launcher.log`,
`artifacts/explicit-update-distribution.log` and
`artifacts/launcher-ui-checks/party-room-update.png`.

## Selected branding and Party Stage integration — September 29, 2026

- [x] Embed the finalized Clover Peak emblem and Leafcut lettering as exact SVG paths.
- [x] Preserve the flat logo geometry, colors and negative space; omit texture/outline effects.
- [x] Replace the hero with the approved four-world V4 composite, preserving local repairs.
- [x] Enlarge the upper-left logo and stage; use horizontal branding in Settings/sidebar views.
- [x] Add the sand action strip and bottom-right 440×76 Play target (400×64 compact).
- [x] Share one split-button edge, highlight and coral shadow with hover/press/disabled states.
- [x] Retain current capsule instance cards, contextual version, Settings toggle and update guards.
- [x] Verify the focused launcher suite and inspect home, hover, Settings, Running, dropdown,
      instance and narrow-window renders, including narrow sidebar layout.
- [x] Verify an isolated native Windows desktop preview: SVG/art rendering, working-area
      placement, both sidebar modes, Settings toggle and Back deselection. The 1160×720
      client fits at (60,16) on the VM's 1280×752 working area in desktop session 6.
- [x] Close the owned preview, remove its temporary task and stop the idle VM.
- [x] Publish Beta 27. This UI pass does not change updater or gameplay code.
      Full multiplayer/installed-update suites were not repeated locally for the
      visual pass; publication CI passed those checks on both platforms.

Evidence lives under ignored `artifacts/launcher-ui-checks/` and
`artifacts/windows-vm/launcher-branding-desktop/`. Protected emblem and wordmark
SHA-256 values still match `assets/brand/status.json`.

[Beta 27](https://github.com/ConcosHut/LuckerParty/releases/tag/v0.3.0-beta.27)
was published from commit `3fea2ca` after
[CI](https://github.com/ConcosHut/LuckerParty/actions/runs/36566964202) passed
both platform builds, game/launcher checks, real installation/update checks
and cross-platform draft asset verification. The Windows Setup, Linux AppImage,
full packages, deltas and Beta feeds are public; Beta 26 remains unchanged.

## Composition and Settings follow-up

- [x] Move the main brand to the upper-left and enlarge the arena in both sidebar modes.
- [x] Grow normal Play to 410×64, with a 350×58 compact layout and a flush square chevron.
- [x] Tighten the headline/action grouping and add subtle drawn background accents.
- [x] Give Settings a larger pill target with normal, hover, pressed and selected states.
- [x] Toggle Settings open/closed by click or Space; Back/Escape also clear selection.
- [x] Expand sidebar Show/Close to 44px targets with readable card text.
- [x] Fit and recenter startup windows using the display's actual scaling and working area.
- [x] Verify actual pointer/keyboard toggling, page visibility and split-button geometry.
- [x] Verify native Windows placement, both sidebar modes and Settings selection.
- [x] Verify installed updates on Windows and Linux and publish the resulting Beta.

## Window, composition and brand — layout agent

- [x] Extend the sand sidebar to the top and left window edges with custom chrome.
- [x] Preserve usable drag, resize, minimize, maximize, close and keyboard behavior.
- [x] Use an asymmetric logo/arena composition with the sidebar, a compact centered
      composition without it, and a usable small-window layout.
- [x] Strengthen logo/headline proportions and supporting typography.
- [x] Restore the pink/orange/purple square brand accents in both wordmarks.
- [x] Move the single installed version and update status below the top-right
      Stable/Beta selector; remove the footer copy.
- [x] Keep Settings navigation and the footer quiet and aligned.

## Controls and visual states — controls agent + integration

- [x] Own normal, hover, pressed, disabled and keyboard-focus button visuals.
- [x] Keep Play hover/pressed coral; remove default gray Fluent overrides.
- [x] Use one segmented channel pill with a sliding thumb, white selected text
      and navy unselected text, including when switching is disabled.
- [x] Explain disabled channel switching with a tooltip.
- [x] Make the Play/chevron sections flush and equal-height with a narrow,
      evenly padded chevron, one outer rounded shape and a subtle divider.
- [x] Style the installed-version dropdown consistently with the launcher.
- [x] Show Running when a single-instance game blocks Play; retain distinct
      checking/downloading/applying/error states and enabled multi-instance Play.
- [x] Recompute helper text immediately on preference changes; show Opens another
      game window only when the opt-in is on and an owned game is running.

## Sidebar and surface polish — sidebar agent

- [x] Use shaded capsule thumbnails on tinted tiles matching the arena art.
- [x] Add running/closing/failure status indicators to instance cards.
- [x] Add eye and close icons to the individual actions.
- [x] Strengthen card hierarchy and place the count badge near its heading.
- [x] Add restrained borders/shadows to cards and controls.
- [x] Add a green check to Up to date, preserving explicit update/error states.

## Integration and verification — primary agent

- [x] Review the combined implementation against both approved mockups.
- [x] Verify settings persistence, immediate helper changes, Running/Play states,
      segmented selection and hover/focus behavior, and split-button geometry.
- [x] Render both sidebar modes, Settings, small window, hover and dropdown states.
- [x] Preserve targeted Show/Close, force-close recovery and all-child update guards.
- [x] Run game/launcher checks and real installed updates on Windows and Linux.
- [x] Verify Windows native custom chrome and DPI layout without interrupting
      an active playtest; identify any unverified native window behavior.
- [x] Update documentation/assets provenance, commit, publish a new Beta, and
      update/open it on Windows when no active playtest blocks replacement.

Optional design copy such as Push / Dodge / Outplay / Together is not required
for this pass. Framework migration is deferred; the current UI gaps can be
addressed with complete Avalonia control themes and custom window chrome.

## Verification recorded September 28, 2026

- `python tools/dev.py check` passed, including the rendered UI and 45 actual
  host/client multiplayer scenarios. Pointer hover, keyboard Space/Escape,
  disabled channel colors, split geometry and immediate settings changes passed.
- `python tools/check_distribution.py --target linux` passed real install,
  failure/recovery, channel changes, updater restart and preference retention.
- An isolated Windows 11 preview at 144 DPI (150%) passed resize, minimize,
  maximize/restore, native system-menu Close, Running/helper changes, two actual
  Godot windows and targeted Show/Close. Native Close preserves the live-game
  guard. HTMAXBUTTON was verified; the Snap flyout and physical drag/Alt+F4
  interaction still benefit from human testing. The preview was removed from
  the desktop and its temporary task unregistered.
- Captures use ignored `artifacts/launcher-ui-checks/` and `windows-polish/`.
  The final wide composition was reviewed separately from the native frame test.
- [Beta 25 CI](https://github.com/ConcosHut/LuckerParty/actions/runs/36500754878)
  passed both platform builds, game/launcher checks, real installation/update
  checks and publication for source commit `8b726f0`.
- [Beta 25](https://github.com/ConcosHut/LuckerParty/releases/tag/v0.3.0-beta.25)
  was installed through the actual Windows updater. Restart intent was consumed
  without starting a game. The installed native launcher repeated the 150% DPI,
  frame, two-instance, Running/helper and targeted Show/Close checks successfully.
  Its temporary test task was removed; the player launcher remains open in
  desktop session 1 with no games running. Final native captures are under
  ignored `artifacts/launcher-ui-checks/windows-beta25/`.

## Follow-up found in the Windows VM

- [x] Recenter or clamp the window position after its startup size is reduced
  to fit the working area. On the VM's 1280×800 desktop, the 1240×840 initial
  sidebar window shrinks to 1240×720 but retains its original centered position,
  leaving the caption buttons above the screen. The client capture renders
  correctly and Play is ready; the console capture exposes the positioning issue.
  Initial evidence: ignored `artifacts/windows-vm/launcher-desktop-trial/`.
  `FitWindowToWorkingArea()` now computes logical sizes by dividing by display
  scaling and centers the final size in the screen's pixel working area.

## Follow-up verification

- The launcher integration checks pass real pointer/Space Settings activation,
  repeated-click close, persistent lilac selection after pointer exit, Back and
  Escape synchronization, enlarged/flush split-button geometry, three concurrent
  children, and targeted Show/Close.
- `python tools/check_distribution.py --target linux` passed actual installed
  updates, failures/recovery, channel transitions and preference retention.
- Windows VM run `run-1790648468147440715` passed both `dev.py check` (including
  all 45 multiplayer scenarios) and actual installed-update checks in session 5.
- A native desktop task verified Settings open/close, Back deselection, both
  sidebar modes and an enabled Play control. The 1240×720 client now sits at
  (20,16), fully inside the VM's 1280×752 working area, with visible captions.
- The shorter-window composition was refined after that package run, rebuilt
  separately in an isolated preview and verified on the same desktop. Final
  captures/status are under ignored `artifacts/windows-vm/launcher-final-desktop/`;
  headless large, small, selected, hover and multi-instance captures are under
  `artifacts/launcher-ui-checks/`.

- [Beta 26 CI](https://github.com/ConcosHut/LuckerParty/actions/runs/36513208552)
  passed game/launcher checks, actual installation/update checks and packaging
  on both platforms for commit `ce3a54e`, then published both together. The first
  Windows attempt stopped on a Godot editor `_EDITOR_GET` error during its
  second import; a failed-job retry passed without changing or suppressing checks.
- [Beta 26](https://github.com/ConcosHut/LuckerParty/releases/tag/v0.3.0-beta.26)
  contains the composition and Settings iteration. Both platform packages and
  their checksums are public; Beta 25 remains unchanged.
- The published Windows Setup checksum was verified on box and again in the
  guest, then installed as a separate player app at `C:\LuckerParty\beta26-player`.
  A limited interactive task passed Settings toggle/Back selection, both sidebar
  modes and enabled Play. The 1000×720 client fits at (140,16) in the VM working
  area. Captures/status are under ignored
  `artifacts/windows-vm/launcher-beta26-desktop/`. Its launcher was closed and
  temporary task removed; no physical-PC applications were touched.
