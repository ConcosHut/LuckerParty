# Party UI assets

`Brand.svg` is an unchanged copy of the finalized root
`assets/brand/lucker-party-logo.svg`. Preserve its geometry/colors; refresh this
copy when the canonical asset intentionally changes. Godot imports it natively.

Nunito Sans Regular/Bold and Lilita One Regular are unchanged copies of the
launcher fonts. Their sources and static-instance details are recorded in
`src/LuckerParty.Launcher/Assets/README.md`; SIL OFL notices are in root `licenses/`
and included by the existing distribution packaging. No system font installation
or web request is needed.

`../PartyTheme.tres` owns shared styles; `../PartyShell.tscn` owns the live layout.
Open/run `../ComponentPreview.tscn` to inspect normal, hover, pressed, disabled
and keyboard focus treatments without a multiplayer connection.
