# WorkTrail app icon provenance

The current application identity uses `worktrail-icon-reference.png`, selected and supplied by project owner Umberto Giacobbi on September 23, 2026. The source is a 1254 × 1254 RGB PNG with an ivory briefcase and lilac work trail on black. The source attachment did not include reliable creation metadata, so this record does not assert a specific image model or prompt for the selected file.

`scripts/WorkTrail.ps1 -Action GenerateAssets` converts the black matte to transparency, preserves antialiased edges, and generates the square, target-size, wide, splash, Store-logo, template-compatibility, and ICO assets in `WorkTrail/Assets/`. Small target-size assets use less padding than tile assets so the mark remains legible in the taskbar. The script only replaces generated identity assets.

The owner selected this design after reviewing several concepts generated with the built-in image-generation tool. The exact selected image was supplied as an attachment and kept unchanged as the master; the generated Windows assets are mechanical derivatives. Previous TrackMeUp icon history remains in `TRACKMEUP_ICON_PROVENANCE.md` and `trackmeup-icon-reference.png`.

The WorkTrail name and app identity artwork are Brand Assets governed by [TRADEMARKS.md](../../TRADEMARKS.md), outside the repository's MIT grant. See [ASSET_LICENSING.md](../../ASSET_LICENSING.md).
