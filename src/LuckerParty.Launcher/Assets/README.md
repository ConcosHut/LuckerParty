# Party Room assets

`party-room-arena.png` is the generated production hero illustration. It contains
no controls or text; the launcher draws every interactive element natively.
Created with the built-in imagegen tool in edit/reference mode, using the approved
Party Room mockup as a style reference on 2026-09-28. Original output:
`/home/para/.codex/generated_images/01a0e67b-6c75-7481-92ca-04b7b3ae947c/exec-815e7c2d-d760-4e51-8f2b-ba61c28d78d4.png`.

Exact prompt:

> Use case: stylized-concept. Asset type: production raster illustration for the Lucker Party desktop launcher. Input image is a style and composition REFERENCE, not a screenshot to reproduce. Generate ONLY the central isometric game arena illustration from this reference, without ANY user interface, text, logos, lettering, title, slogan, buttons, border, or window chrome. Three friendly polished smooth capsule characters with small black vertical eyes: pink at front-left, orange at back-center, purple at front-right, in an isometric light-gray tiled square arena with short simple gray perimeter walls and three pink/orange/purple colored cubes. Match the playful clean clay-like 3D rendering and excellent warm soft shadows of the reference. Plain solid creamy ivory background color #FFFDF8, fading soft ground shadows to the same background; no horizon, no room, no props outside the arena. Wide landscape canvas, arena centered and fills most of width; entire arena visible with modest margins, visually compact vertically, illustration about twice as wide as tall. Absolutely zero lettering anywhere, especially walls. This is the actual hero image that will ship inside the app, not a mockup of the app.

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
