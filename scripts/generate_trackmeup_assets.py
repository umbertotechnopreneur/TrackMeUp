"""Generate Microsoft Store-ready TrackMeUp visual assets from the approved artwork."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "TrackMeUp" / "Assets"
REFERENCE = ROOT / "design" / "branding" / "trackmeup-icon-reference.png"
SCALES = (100, 125, 150, 200, 400)
TARGET_SIZES = (16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256)
REFERENCE_SIZE = (1536, 1024)
MASTER_BOX = (165, 225, 735, 795)
COMPACT_BOX = (1075, 395, 1305, 625)


def _scaled_box(source: Image.Image, box: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    """Convert reference-image coordinates to the current source resolution."""

    width_ratio = source.width / REFERENCE_SIZE[0]
    height_ratio = source.height / REFERENCE_SIZE[1]
    return tuple(round(value * ratio) for value, ratio in zip(box, (width_ratio, height_ratio, width_ratio, height_ratio)))


def _extract_icon(source: Image.Image, box: tuple[int, int, int, int]) -> Image.Image:
    """Extract one approved icon and replace its neutral concept-board background with alpha."""

    icon = source.crop(_scaled_box(source, box)).convert("RGBA")
    pixels = icon.load()
    for y in range(icon.height):
        for x in range(icon.width):
            red, green, blue, alpha = pixels[x, y]
            if min(red, green, blue) >= 236:
                pixels[x, y] = (red, green, blue, 0)

    bounds = icon.getbbox()
    return icon.crop(bounds) if bounds else icon


def _pixel_size(base_size: int, scale: int) -> int:
    """Return a manifest asset size using Microsoft Store scale qualifiers."""

    return (base_size * scale + 50) // 100


def _fit(icon: Image.Image, size: tuple[int, int], padding: float = 0.06) -> Image.Image:
    """Center an icon with safe padding inside a transparent package-asset canvas."""

    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    available_width = max(1, round(size[0] * (1 - padding * 2)))
    available_height = max(1, round(size[1] * (1 - padding * 2)))
    fitted = icon.copy()
    fitted.thumbnail((available_width, available_height), Image.Resampling.LANCZOS)
    offset = ((size[0] - fitted.width) // 2, (size[1] - fitted.height) // 2)
    canvas.alpha_composite(fitted, offset)
    return canvas


def _themed_canvas(size: tuple[int, int], icon: Image.Image, theme: str) -> Image.Image:
    """Create an opaque tile or splash asset while keeping the approved icon untouched."""

    backgrounds = {"default": "#112235", "dark": "#314157", "light": "#F8F4ED"}
    canvas = Image.new("RGBA", size, backgrounds[theme])
    fitted = _fit(icon, size, 0.14)
    canvas.alpha_composite(fitted)
    return canvas


def _save(image: Image.Image, path: Path) -> None:
    """Persist a PNG package asset without palette conversion artifacts."""

    image.save(path, format="PNG")


def _clear_previous_assets() -> None:
    """Remove only generated TrackMeUp package outputs before creating the replacement set."""

    # Asset generation is isolated to the TrackMeUp prefix so unrelated artwork is never removed.
    for path in ASSETS.glob("TrackMeUp*.png"):
        path.unlink()
    icon_file = ASSETS / "TrackMeUpIcon.ico"
    if icon_file.exists():
        icon_file.unlink()
    for name in (
        "LockScreenLogo.scale-200.png",
        "SplashScreen.scale-200.png",
        "Square150x150Logo.scale-200.png",
        "Square44x44Logo.scale-200.png",
        "Square44x44Logo.targetsize-24_altform-unplated.png",
        "StoreLogo.png",
        "Wide310x150Logo.scale-200.png",
    ):
        path = ASSETS / name
        if path.exists():
            path.unlink()


def _write_square_assets(master: Image.Image, compact: Image.Image) -> None:
    """Write scaled tile and complete app-list target-size variants."""

    _save(_fit(compact, (44, 44)), ASSETS / "TrackMeUpSquare44Logo.png")
    _save(_fit(master, (150, 150)), ASSETS / "TrackMeUpSquare150Logo.png")
    _save(_fit(master, (50, 50)), ASSETS / "TrackMeUpStoreLogo.png")

    for scale in SCALES:
        _save(_fit(compact, (_pixel_size(44, scale),) * 2), ASSETS / f"TrackMeUpSquare44Logo.scale-{scale}.png")
        _save(_fit(compact, (_pixel_size(44, scale),) * 2), ASSETS / f"TrackMeUpSquare44Logo.scale-{scale}_altform-colorful_theme-light.png")
        _save(_fit(compact, (_pixel_size(44, scale),) * 2), ASSETS / f"TrackMeUpSquare44Logo.scale-{scale}_altform-colorful_theme-dark.png")
        _save(_fit(master, (_pixel_size(150, scale),) * 2), ASSETS / f"TrackMeUpSquare150Logo.scale-{scale}.png")
        _save(_fit(master, (_pixel_size(150, scale),) * 2), ASSETS / f"TrackMeUpSquare150Logo.scale-{scale}_altform-colorful_theme-light.png")
        _save(_fit(master, (_pixel_size(150, scale),) * 2), ASSETS / f"TrackMeUpSquare150Logo.scale-{scale}_altform-colorful_theme-dark.png")
        _save(_fit(master, (_pixel_size(50, scale),) * 2), ASSETS / f"TrackMeUpStoreLogo.scale-{scale}.png")
        _save(_fit(master, (_pixel_size(50, scale),) * 2), ASSETS / f"TrackMeUpStoreLogo.scale-{scale}_altform-colorful_theme-light.png")
        _save(_fit(master, (_pixel_size(50, scale),) * 2), ASSETS / f"TrackMeUpStoreLogo.scale-{scale}_altform-colorful_theme-dark.png")

    for size in TARGET_SIZES:
        source = master if size == 256 else compact
        icon = _fit(source, (size, size), 0.04)
        _save(icon, ASSETS / f"TrackMeUpSquare44Logo.targetsize-{size}.png")
        _save(icon, ASSETS / f"TrackMeUpSquare44Logo.targetsize-{size}_altform-unplated.png")
        _save(icon, ASSETS / f"TrackMeUpSquare44Logo.targetsize-{size}_altform-lightunplated.png")


def _write_wide_and_splash_assets(master: Image.Image) -> None:
    """Write Windows 10 compatibility tiles and light/dark splash-screen variants."""

    _save(_themed_canvas((310, 150), master, "default"), ASSETS / "TrackMeUpWide310x150Logo.png")
    _save(_themed_canvas((620, 300), master, "default"), ASSETS / "TrackMeUpSplashScreen.png")

    for scale in SCALES:
        wide_size = (_pixel_size(310, scale), _pixel_size(150, scale))
        splash_size = (_pixel_size(620, scale), _pixel_size(300, scale))
        _save(_themed_canvas(wide_size, master, "default"), ASSETS / f"TrackMeUpWide310x150Logo.scale-{scale}.png")
        _save(_themed_canvas(wide_size, master, "light"), ASSETS / f"TrackMeUpWide310x150Logo.scale-{scale}_altform-colorful_theme-light.png")
        _save(_themed_canvas(wide_size, master, "dark"), ASSETS / f"TrackMeUpWide310x150Logo.scale-{scale}_altform-colorful_theme-dark.png")
        _save(_themed_canvas(splash_size, master, "default"), ASSETS / f"TrackMeUpSplashScreen.scale-{scale}.png")
        _save(_themed_canvas(splash_size, master, "light"), ASSETS / f"TrackMeUpSplashScreen.scale-{scale}_altform-colorful_theme-light.png")
        _save(_themed_canvas(splash_size, master, "dark"), ASSETS / f"TrackMeUpSplashScreen.scale-{scale}_altform-colorful_theme-dark.png")


def _write_template_compatibility_assets(master: Image.Image, compact: Image.Image) -> None:
    """Replace the WinUI template placeholders with branded compatibility assets."""

    _save(_fit(compact, (88, 88)), ASSETS / "Square44x44Logo.scale-200.png")
    _save(_fit(compact, (24, 24)), ASSETS / "Square44x44Logo.targetsize-24_altform-unplated.png")
    _save(_fit(master, (300, 300)), ASSETS / "Square150x150Logo.scale-200.png")
    _save(_fit(master, (50, 50)), ASSETS / "StoreLogo.png")
    _save(_themed_canvas((620, 300), master, "default"), ASSETS / "Wide310x150Logo.scale-200.png")
    _save(_themed_canvas((1240, 600), master, "default"), ASSETS / "SplashScreen.scale-200.png")
    _save(_fit(compact, (48, 48)), ASSETS / "LockScreenLogo.scale-200.png")


def _write_ico(master: Image.Image, compact: Image.Image) -> None:
    """Create an executable icon with exact shell-size frames and a detailed 256px frame."""

    frames = [_fit(master if size == 256 else compact, (size, size), 0.04) for size in TARGET_SIZES]
    # Pillow retains same-size appended frames, preserving the compact artwork at shell sizes.
    frames[-1].save(ASSETS / "TrackMeUpIcon.ico", format="ICO", sizes=[(size, size) for size in TARGET_SIZES], append_images=frames[:-1])


def main() -> None:
    """Generate all package assets from the approved logo reference image."""

    if not REFERENCE.is_file():
        raise FileNotFoundError(f"Approved TrackMeUp artwork is missing: {REFERENCE}")

    ASSETS.mkdir(parents=True, exist_ok=True)
    source = Image.open(REFERENCE)
    master = _extract_icon(source, MASTER_BOX)
    compact = _extract_icon(source, COMPACT_BOX)
    _clear_previous_assets()
    _write_square_assets(master, compact)
    _write_wide_and_splash_assets(master)
    _write_template_compatibility_assets(master, compact)
    _write_ico(master, compact)


if __name__ == "__main__":
    main()
