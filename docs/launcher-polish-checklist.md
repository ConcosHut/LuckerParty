# Party Room polish checklist

Scope: approved Party Room mockups, the screenshot review, and the seven user
additions. Keep Avalonia and the existing C# updater/process controller. Work
in parallel on separate files, then verify the combined result before release.

Implementation and local verification are complete. Windows/Linux release CI
and publication remain pending at this source revision.

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
- [ ] Run game/launcher checks and real installed updates on Windows and Linux.
- [x] Verify Windows native custom chrome and DPI layout without interrupting
      an active playtest; identify any unverified native window behavior.
- [ ] Update documentation/assets provenance, commit, publish a new Beta, and
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
