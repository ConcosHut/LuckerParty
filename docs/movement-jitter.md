# High-refresh movement jitter — prototype 01.1

## Report and diagnosis

The Windows playtest ran at 240 FPS on a 240 Hz monitor. Holding A/D with the
mouse still caused visible jitter/persistence at box edges.

The initial controller moved the physics body at Godot's default 60 Hz. Its
child camera inherited that unsmoothed position. At 240 FPS, each 0.1-meter
walking step can remain unchanged across several displayed frames.

This is a reproducible software source of stepping; the user's report can also
include monitor response artifacts. The Windows comparison is still needed to
establish how much of the observed effect this change resolves.

## Change

- Explicitly keep physics at 60 Hz and enable Godot physics interpolation.
- Place the camera in global space with automatic camera interpolation disabled.
- Follow the body's interpolated position each rendered frame.
- Apply mouse rotation immediately to the camera, and update the body's yaw
  during physics ticks. Interpolated physics transforms are not changed by
  mouse events outside those ticks.
- Clear interpolation history on spawn/reset so the camera cannot sweep from
  the old position to the spawn point.

This follows Godot's documented first-person camera approach:
[advanced interpolation](https://docs.godotengine.org/en/4.5/tutorials/physics/interpolation/advanced_physics_interpolation.html)
and [interpolation/reset guidance](https://docs.godotengine.org/en/4.5/tutorials/physics/interpolation/using_physics_interpolation.html).

Position interpolation introduces up to roughly one physics tick of visual lag
relative to the current simulated position. Mouse rotation is not delayed.

## Automated comparison

`python tools/dev.py check` runs the controller checks and the camera scenario.
The latter continuously strafes in an unobstructed area at 240 fixed render
frames/second with 60 physics ticks/second. It samples 180 camera positions
after warming up each camera mode.

Observed results:

| Camera position mode | Stationary sampled frames | Largest frame step |
| --- | --- | --- |
| Original/raw | 135 / 180 | 0.10000 m |
| Interpolated | 0 / 180 | 0.02500 m |

All 15 movement/camera checks passed, including immediate mouse rotation and
clearing camera history after resetting. Fixed-rate headless samples verify
camera positions; they do not measure a Windows monitor's displayed frames.
The same 240-fixed-FPS comparison passed in a graphical Linux run as well.
Both scenarios also passed in the exported Linux release. Windows and Linux
01.1 ZIPs were exported successfully; the Windows ZIP and bundled runtime were
validated. The new Windows comparison remains for the user to playtest.

## Windows debugging controls

- F3: show average, p95, and maximum frame times over the last 240 frames.
- F4: toggle camera position interpolation without changing movement physics.
- F5: switch between normal 60 Hz physics and exaggerated 10 Hz diagnostic
  physics. The low rate is only for observing interpolation, not normal play.

Hold A/D with the mouse still and watch the same box edge while toggling F4.
Keep physics at 60 Hz for the primary comparison. If interpolation helps and
frame times stay stable, the software stepping diagnosis is supported. If
artifacts remain, distinguish positional jumps/frame-time spikes from trailing
images; then investigate display response or presentation timing accordingly.
