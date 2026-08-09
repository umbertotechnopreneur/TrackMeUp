from __future__ import annotations

import hashlib
import json
import os
import struct
from pathlib import Path

from PIL import Image, ImageCms, ImageDraw, ImageFilter, ImageFont, PngImagePlugin


ROOT = Path(__file__).resolve().parent
SOURCE_DIR = ROOT / "source"
OUTPUT_DIR = ROOT / "output"

SOURCE_FILES = {
    "dark": SOURCE_DIR / "trackmeup-recall-timeline-theme-dark-source.png",
    "light": SOURCE_DIR / "trackmeup-recall-timeline-theme-light-source.png",
}

THEME_COLORS = {
    "dark": {
        "wordmark": (236, 242, 248, 255),
        "coral": (249, 102, 91, 255),
    },
    "light": {
        "wordmark": (17, 34, 53, 255),
        "coral": (232, 86, 76, 255),
    },
}

MASTER_SIZE = (3840, 1280)
README_SIZE = (2400, 800)
SQUARE_SIZE = (1024, 1024)
WORDMARK_SIZE = (2400, 600)


def srgb_profile() -> bytes:
    profile = ImageCms.ImageCmsProfile(ImageCms.createProfile("sRGB"))
    return profile.tobytes()


def png_metadata(title: str, description: str) -> PngImagePlugin.PngInfo:
    info = PngImagePlugin.PngInfo()
    info.add_text("Title", title)
    info.add_text("Description", description)
    info.add_text("Software", "TrackMeUp recall-timeline asset renderer")
    info.add_text("Provenance", "AI-generated source artwork; deterministic crop, color, typography, and export refinement")
    return info


def save_rgba(image: Image.Image, path: Path, *, title: str, description: str) -> None:
    rgba = image.convert("RGBA")
    rgba.save(
        path,
        format="PNG",
        optimize=True,
        compress_level=9,
        dpi=(96, 96),
        icc_profile=srgb_profile(),
        pnginfo=png_metadata(title, description),
    )


def font_path() -> Path:
    windows_root = Path(os.environ.get("WINDIR", r"C:\Windows"))
    candidate = windows_root / "Fonts" / "bahnschrift.ttf"
    if not candidate.is_file():
        raise FileNotFoundError(f"Required Windows font not found: {candidate}")
    return candidate


def bahnschrift_semibold(size: int) -> ImageFont.FreeTypeFont:
    font = ImageFont.truetype(str(font_path()), size=size)
    try:
        font.set_variation_by_name("SemiBold")
    except (AttributeError, OSError, ValueError):
        # Bahnschrift normally exposes named variations; regular remains a safe renderer fallback.
        pass
    return font


def crop_to_three_by_one(source: Image.Image) -> Image.Image:
    crop_height = source.width // 3
    if crop_height > source.height:
        raise ValueError(f"Source is too wide to crop to 3:1: {source.size}")
    top = (source.height - crop_height) // 2
    return source.crop((0, top, source.width, top + crop_height))


def refined_art(theme: str) -> Image.Image:
    with Image.open(SOURCE_FILES[theme]) as source:
        cropped = crop_to_three_by_one(source.convert("RGB"))
        master = cropped.resize(MASTER_SIZE, Image.Resampling.LANCZOS)
    # A restrained pass restores edge definition after enlargement without changing the artwork.
    master = master.filter(ImageFilter.UnsharpMask(radius=1.2, percent=65, threshold=3))
    return master.convert("RGBA")


def draw_split_wordmark(canvas: Image.Image, theme: str, *, origin: tuple[int, int], font_size: int) -> None:
    draw = ImageDraw.Draw(canvas)
    font = bahnschrift_semibold(font_size)
    x, y = origin
    first = "TrackMe"
    second = "Up"
    draw.text((x, y), first, font=font, fill=THEME_COLORS[theme]["wordmark"], anchor="lt")
    second_x = x + round(draw.textlength(first, font=font))
    draw.text((second_x, y), second, font=font, fill=THEME_COLORS[theme]["coral"], anchor="lt")


def banner_with_wordmark(art: Image.Image, theme: str) -> Image.Image:
    result = art.copy()
    draw_split_wordmark(result, theme, origin=(176, 116), font_size=220)
    return result


def square_mark(art: Image.Image) -> Image.Image:
    # The crop preserves the coral retrieval node, lifted page, and enough surrounding timeline context.
    left = 760
    top = 0
    box_size = 1280
    crop = art.crop((left, top, left + box_size, top + box_size))
    return crop.resize(SQUARE_SIZE, Image.Resampling.LANCZOS).filter(
        ImageFilter.UnsharpMask(radius=0.8, percent=45, threshold=3)
    )


def transparent_wordmark(theme: str) -> Image.Image:
    canvas = Image.new("RGBA", WORDMARK_SIZE, (0, 0, 0, 0))
    font_size = 390
    font = bahnschrift_semibold(font_size)
    draw = ImageDraw.Draw(canvas)
    first = "TrackMe"
    second = "Up"
    width = draw.textlength(first + second, font=font)
    bbox = draw.textbbox((0, 0), first + second, font=font, anchor="lt")
    height = bbox[3] - bbox[1]
    origin = (round((WORDMARK_SIZE[0] - width) / 2), round((WORDMARK_SIZE[1] - height) / 2 - bbox[1]))
    draw_split_wordmark(canvas, theme, origin=origin, font_size=font_size)
    return canvas


def png_ihdr(path: Path) -> dict[str, int]:
    with path.open("rb") as stream:
        signature = stream.read(8)
        if signature != b"\x89PNG\r\n\x1a\n":
            raise ValueError(f"Not a PNG: {path}")
        length = struct.unpack(">I", stream.read(4))[0]
        chunk_type = stream.read(4)
        if length != 13 or chunk_type != b"IHDR":
            raise ValueError(f"Unexpected PNG header: {path}")
        width, height, bit_depth, color_type, _, _, _ = struct.unpack(">IIBBBBB", stream.read(13))
    return {
        "width": width,
        "height": height,
        "bit_depth": bit_depth,
        "color_type": color_type,
    }


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def render() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    outputs: list[Path] = []

    for theme in ("dark", "light"):
        art = refined_art(theme)
        banner = banner_with_wordmark(art, theme)
        readme_banner = banner.resize(README_SIZE, Image.Resampling.LANCZOS)
        mark = square_mark(art)
        wordmark = transparent_wordmark(theme)

        generated = {
            OUTPUT_DIR / f"trackmeup-recall-timeline-art-theme-{theme}-master-3840x1280.png": (
                art,
                f"TrackMeUp recall timeline artwork, {theme} theme",
                "Art-only master with the selected page retrieval gesture.",
            ),
            OUTPUT_DIR / f"trackmeup-recall-timeline-banner-theme-{theme}-master-3840x1280.png": (
                banner,
                f"TrackMeUp recall timeline banner, {theme} theme",
                "Master 3:1 banner with exact TrackMeUp wordmark.",
            ),
            OUTPUT_DIR / f"trackmeup-recall-timeline-banner-theme-{theme}-readme-2400x800.png": (
                readme_banner,
                f"TrackMeUp recall timeline README banner, {theme} theme",
                "GitHub README 3:1 banner with exact TrackMeUp wordmark.",
            ),
            OUTPUT_DIR / f"trackmeup-recall-timeline-mark-theme-{theme}-1024x1024.png": (
                mark,
                f"TrackMeUp recall timeline square mark, {theme} theme",
                "Square crop focused on the rediscovered-page gesture.",
            ),
            OUTPUT_DIR / f"trackmeup-wordmark-for-theme-{theme}-transparent-2400x600.png": (
                wordmark,
                f"TrackMeUp transparent wordmark for {theme} backgrounds",
                "Transparent TrackMeUp wordmark; Up uses the coral retrieval accent.",
            ),
        }

        for path, (image, title, description) in generated.items():
            save_rgba(image, path, title=title, description=description)
            outputs.append(path)

    dark_preview = Image.open(
        OUTPUT_DIR / "trackmeup-recall-timeline-banner-theme-dark-readme-2400x800.png"
    ).convert("RGBA")
    light_preview = Image.open(
        OUTPUT_DIR / "trackmeup-recall-timeline-banner-theme-light-readme-2400x800.png"
    ).convert("RGBA")
    preview = Image.new("RGBA", (2400, 1600), (255, 255, 255, 255))
    preview.alpha_composite(dark_preview, (0, 0))
    preview.alpha_composite(light_preview, (0, 800))
    preview_path = OUTPUT_DIR / "trackmeup-recall-timeline-theme-pair-preview-2400x1600.png"
    save_rgba(
        preview,
        preview_path,
        title="TrackMeUp recall timeline dark and light preview",
        description="Stacked preview of the selected dark and light README banners.",
    )
    outputs.append(preview_path)

    manifest = {
        "concept": "Recall Timeline",
        "status": "selected direction, refined",
        "wordmark": {
            "text": "TrackMeUp",
            "font": "Bahnschrift SemiBold",
            "accent": "Up in coral",
        },
        "format": "PNG truecolor RGBA, 8 bits per channel, embedded sRGB profile",
        "outputs": [],
    }
    for path in sorted(outputs):
        header = png_ihdr(path)
        if header["bit_depth"] != 8 or header["color_type"] != 6:
            raise ValueError(f"Expected 32-bit RGBA PNG, got {header}: {path}")
        manifest["outputs"].append(
            {
                "file": path.relative_to(ROOT).as_posix(),
                **header,
                "sha256": sha256(path),
            }
        )

    (ROOT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    render()
