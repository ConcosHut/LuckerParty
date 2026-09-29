# Party Room assets

`party-room-arena.png` is the approved 3548 × 1774 four-world hero illustration.
It contains no controls or text; the launcher draws every interactive element
natively. SHA-256:
`edcde5a500e6466f00136d7afa58faf56136511b3d053d16567b4c980eceaf97`.

The scenes show Source-style balancing platforms and boulders, competitive
fishing, Warcraft-style Sheep Shot, and MapleStory-style King Slime combat.
They were generated independently with the built-in imagegen tool, then
assembled as layers. Local hand/rod repairs, sandstone-wall/shadow repairs and
the final CSS boundary correction are preserved in this exact V4 composite.
Do not regenerate the whole illustration for a small regional correction.

The ignored working master, masks, scripts, generation prompts and comparison
captures live under `artifacts/brand-exploration/research-round-v7/` in
`hero-layered/`, `hero-layered-v2/`, `hero-seam-repairs-v3/` and
`hero-boundary-v4/`. The portable production PNG is bundled here; generation
and assembly tools are not build dependencies. See
[the brand study](../../../docs/brand-reference-study.md) for decisions/history.
This replaces the earlier three-capsule arena hero.

The finalized logo sources are under root `assets/brand/`. The launcher embeds
the actual SVG files and `BrandLogo` renders their closed paths, inherited fills
and translate/scale transforms directly through Avalonia. Stacked and horizontal
lockups preserve the protected emblem and Leafcut lettering without text fonts,
texture effects or a raster conversion.

Fonts come from Google Fonts commit `23e54b51ddffbc7713c583748e3bd86f62b1fa4a`:

- `ofl/lilitaone/LilitaOne-Regular.ttf`, SHA256
  `f5b641c45c69d772ee4eda687bc9fda411d5cad6b0b45371491da4580cbc8d59`.
- `ofl/nunitosans/NunitoSans[YTLC,opsz,wdth,wght].ttf`, source SHA256
  `f934d7142fb4784bf828da485b7dcbd90c0c80d514e9d49a5da0ed3a1ae2491d`.
  Bundled Regular, Bold and ExtraBold are static instances generated with
  FontTools at wght=400/700/800, wdth=100, opsz=12, YTLC=500, using
  `instantiateVariableFont(..., updateFontNames=True)`. Static instances avoid
  platform-dependent variable-font weight selection.

Both families use SIL OFL 1.1. The complete notices are included under root
`licenses/` and copied into distributed packages. Fonts are embedded resources;
no system font installation or network access is required.
