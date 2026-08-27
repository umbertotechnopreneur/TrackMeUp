#!/usr/bin/env python3
"""Build TrackMeUp's distributable capital-city and skyline catalog.

The script downloads GeoNames' CC BY 4.0 gazetteer and freely licensed
Wikimedia Commons thumbnails, then writes a deterministic SQLite catalog,
optimized WebP assets, and a human-readable attribution manifest.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import hashlib
import html
import io
import json
import re
import shutil
import sqlite3
import time
import urllib.parse
import urllib.error
import urllib.request
import unicodedata
import zipfile
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageOps


USER_AGENT = "TrackMeUpWorldClockCatalog/1.0 (https://github.com/umbertogiacobbidotbiz/TrackMeUp)"
GEONAMES_URL = "https://download.geonames.org/export/dump/cities15000.zip"
GEONAMES_COUNTRIES_URL = "https://download.geonames.org/export/dump/countryInfo.txt"
COMMONS_API = "https://commons.wikimedia.org/w/api.php"
CAPITAL_COUNT = 100
LOCAL_CITY_GEONAME_ID = 1566083  # Ho Chi Minh City, retained as the local mockup city.
ALLOWED_LICENSE_PREFIXES = ("CC BY ", "CC BY-SA ", "CC0")
ALLOWED_PUBLIC_DOMAIN_NAMES = {"Public domain", "PDM", "CC-PD-Mark"}
CITY_SEARCH_ALIASES = {"nay-pyi-taw": "Naypyidaw"}
EXCLUDED_TITLE_TERMS = (
    "flag", "coat of arms", "logo", "locator map", "location map", "seal of", "icon",
    "airport", "metro map", "street map", "district map", "football", "passport",
    "stadium", "sport", "mural", "painting", "poster", "festival", "ceremony",
    "interior", "inside", "mosque", "temple", "mausoleum", "monument", "bridge",
    "park", "garden", "museum exhibit",
    "attack", "battle", "illustration", "outline", "engraving", "etching", "postcard",
    "historic print", "historical print",
    "university", "campus", "school", "classroom", "construction", "facility",
    "students", "ceremonial", "group photo", "portrait", "selfie",
    "satellite", "copernicus",
    "president", "secretary", "minister", "shakes hands", "bilateral meeting",
    "sculpture", "statue", "camel", "herd", "bombardment", "earthquake", "rubble",
    "disaster", "resort", "space station", "iss0", "plan de la ville", "estampe",
    "gate", "cafe", "caffis", "coffee", "restaurant", "chapel", "fountain",
    "military", "army", "soldier", "black hawk", "helicopter", "aircraft", "airplane",
    "airbase", "air base", "airfield", "runway", "bus", "coach", "pigeon", "bird",
    "goat", "chevres", "protest", "sanction", "market", "street scene", "mairie",
    "hotel", "national bank", "farm", "baywalk", "portal de las americas", "ba dinh",
    "tordenskiold", "hluttaw", "nawabad", "avila mt", "entre ankara", "between ankara",
)
SKYLINE_TERMS = (
    "skyline", "panorama", "panoramic", "cityscape", "aerial", "downtown",
    "city centre", "city center", "urban", "high-rise", "skyscraper",
    "overhead", "aeriel", "view over", "city from",
    "vue aérienne", "vue aerienne", "vue panoramique", "panoramique", "centre-ville",
    "vue sur",
    "ville de", "sunset in",
    "buildings in", "from the air",
)


@dataclass(frozen=True)
class City:
    geoname_id: int
    slug: str
    name: str
    ascii_name: str
    country_code: str
    country_name: str
    latitude: float
    longitude: float
    population: int
    timezone_id: str
    is_capital: bool


@dataclass(frozen=True)
class CommonsAsset:
    title: str
    source_url: str
    download_url: str
    author: str
    license_name: str
    license_url: str
    description: str
    width: int
    height: int


@dataclass(frozen=True)
class PreparedCity:
    city: City
    assets: tuple[tuple[str, CommonsAsset, str, Path, str], ...]


def request_bytes(url: str, timeout: int = 45) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    for attempt in range(5):
        try:
            with urllib.request.urlopen(request, timeout=timeout) as response:
                return response.read()
        except (urllib.error.HTTPError, urllib.error.URLError, TimeoutError) as error:
            if attempt == 4:
                raise
            delay = 10 * (attempt + 1) if isinstance(error, urllib.error.HTTPError) and error.code == 429 else 2 ** attempt
            time.sleep(delay)
    raise AssertionError("unreachable")


def slugify(value: str) -> str:
    normalized = value.lower().encode("ascii", "ignore").decode("ascii")
    return re.sub(r"[^a-z0-9]+", "-", normalized).strip("-")


def plain_text(value: str | None) -> str:
    if not value:
        return ""
    without_tags = re.sub(r"<[^>]+>", " ", html.unescape(value))
    return re.sub(r"\s+", " ", without_tags).strip()


def folded_text(value: str) -> str:
    ascii_value = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii").lower()
    return re.sub(r"[^a-z0-9]+", " ", ascii_value).strip()


def metadata_value(metadata: dict, key: str) -> str:
    value = metadata.get(key, {})
    return plain_text(value.get("value") if isinstance(value, dict) else "")


def download_cities(cache_dir: Path) -> list[City]:
    archive_path = cache_dir / "cities15000.zip"
    if not archive_path.exists():
        archive_path.write_bytes(request_bytes(GEONAMES_URL))

    with zipfile.ZipFile(archive_path) as archive:
        txt_name = next(name for name in archive.namelist() if name.endswith(".txt"))
        rows = archive.read(txt_name).decode("utf-8").splitlines()

    country_info_path = cache_dir / "countryInfo.txt"
    if not country_info_path.exists():
        country_info_path.write_bytes(request_bytes(GEONAMES_COUNTRIES_URL))
    countries = {}
    for line in country_info_path.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue
        fields = line.split("\t")
        countries[fields[0]] = fields[4]

    all_cities: list[City] = []
    for row in rows:
        fields = row.split("\t")
        if len(fields) < 19:
            continue
        geoname_id = int(fields[0])
        feature_code = fields[7]
        is_capital = feature_code == "PPLC"
        if not is_capital and geoname_id != LOCAL_CITY_GEONAME_ID:
            continue
        city = City(
            geoname_id=geoname_id,
            slug=slugify(fields[2]),
            name=fields[1],
            ascii_name=fields[2],
            latitude=float(fields[4]),
            longitude=float(fields[5]),
            country_code=fields[8],
            country_name=countries[fields[8]],
            population=int(fields[14] or 0),
            timezone_id=fields[17],
            is_capital=is_capital,
        )
        all_cities.append(city)

    capitals = sorted((city for city in all_cities if city.is_capital), key=lambda c: (-c.population, c.ascii_name))
    selected = capitals[:CAPITAL_COUNT]
    local = next((city for city in all_cities if city.geoname_id == LOCAL_CITY_GEONAME_ID), None)
    if len(selected) != CAPITAL_COUNT or local is None:
        raise RuntimeError("GeoNames did not yield the expected 100 capitals and Ho Chi Minh City.")

    # Slugs are stable application identifiers; fail instead of silently changing a duplicate.
    cities = [local, *selected]
    if len({city.slug for city in cities}) != len(cities):
        raise RuntimeError("Selected city slugs are not unique.")
    return cities


def commons_search(query: str, city_name: str, limit: int = 30) -> list[CommonsAsset]:
    params = {
        "action": "query",
        "format": "json",
        "formatversion": "2",
        "generator": "search",
        "gsrsearch": query,
        "gsrnamespace": "6",
        "gsrlimit": str(limit),
        "prop": "imageinfo",
        "iiprop": "url|mime|mediatype|size|extmetadata",
        "iiurlwidth": "900",
        "iiextmetadatalanguage": "en",
        "iiextmetadatafilter": "Artist|LicenseShortName|LicenseUrl|ImageDescription|ObjectName|Credit|Categories",
        "origin": "*",
    }
    payload = json.loads(request_bytes(f"{COMMONS_API}?{urllib.parse.urlencode(params)}"))
    assets: list[CommonsAsset] = []
    for page in payload.get("query", {}).get("pages", []):
        info = (page.get("imageinfo") or [{}])[0]
        title = page.get("title", "")
        lowered_title = folded_text(title)
        if any(term in lowered_title for term in EXCLUDED_TITLE_TERMS):
            continue
        if info.get("mediatype") != "BITMAP" or info.get("mime") not in {"image/jpeg", "image/png", "image/webp"}:
            continue
        width = int(info.get("width") or 0)
        height = int(info.get("height") or 0)
        if width < 480 or height < 280 or width / max(height, 1) < 1.05:
            continue
        metadata = info.get("extmetadata") or {}
        license_name = metadata_value(metadata, "LicenseShortName")
        if not (license_name.startswith(ALLOWED_LICENSE_PREFIXES) or license_name in ALLOWED_PUBLIC_DOMAIN_NAMES):
            continue
        author = metadata_value(metadata, "Artist") or metadata_value(metadata, "Credit")
        license_url = metadata_value(metadata, "LicenseUrl")
        if not author or (license_name not in ALLOWED_PUBLIC_DOMAIN_NAMES and not license_url):
            continue
        description = metadata_value(metadata, "ImageDescription") or metadata_value(metadata, "ObjectName")
        categories = metadata_value(metadata, "Categories")
        searchable = folded_text(f"{title} {description} {categories}")
        if any(folded_text(term) in searchable for term in EXCLUDED_TITLE_TERMS):
            continue
        searchable_compact = searchable.replace(" ", "")
        visual_compact = folded_text(f"{title} {description}").replace(" ", "")
        city_token = folded_text(city_name).replace(" ", "")
        if (city_token not in searchable_compact
                or not any(folded_text(term).replace(" ", "") in visual_compact for term in SKYLINE_TERMS)
                or re.search(r"\b(?:18|19)\d{2}\b", searchable)):
            continue
        page_path = urllib.parse.quote(title.replace(" ", "_"), safe=":()/,_-'!")
        assets.append(CommonsAsset(
            title=title.removeprefix("File:"),
            source_url=f"https://commons.wikimedia.org/wiki/{page_path}",
            download_url=info.get("thumburl") or info.get("url"),
            author=author,
            license_name=license_name,
            license_url=license_url,
            description=description,
            width=width,
            height=height,
        ))
    return sorted(
        assets,
        key=lambda asset: (
            -sum(
                folded_text(term).replace(" ", "")
                in folded_text(f"{asset.title} {asset.description}").replace(" ", "")
                for term in SKYLINE_TERMS
            ),
            -asset.width * asset.height,
            asset.title,
        ),
    )


def find_assets(city: City) -> tuple[CommonsAsset, CommonsAsset]:
    search_name = CITY_SEARCH_ALIASES.get(city.slug, city.ascii_name)
    country_words = [
        word for word in folded_text(city.country_name).split()
        if word not in {"the", "of", "and", "republic", "democratic", "united", "state", "states", "kingdom", "islands"}
    ]
    country_keyword = country_words[-1] if country_words else city.country_code
    combined_query = (
        f'{search_name} "{country_keyword}" '
        "(skyline OR panorama OR panoramic OR cityscape OR aerial OR downtown OR urban OR \"vue sur\" OR \"ville de\" OR \"sunset in\")"
    )
    candidates = commons_search(combined_query, search_name, limit=50)
    if len({candidate.title for candidate in candidates}) < 2:
        candidates.extend(commons_search(f'{search_name} "{country_keyword}"', search_name, limit=50))
    if len({candidate.title for candidate in candidates}) < 2:
        for focused_query in (
            f'{search_name} city "{country_keyword}"',
            f'{search_name} drone "{country_keyword}"',
            f'{search_name} "vue aerienne"',
            f'{search_name} panorama "{country_keyword}"',
            f'{search_name} buildings "{country_keyword}"',
            f'{search_name} "from the air"',
        ):
            candidates.extend(commons_search(focused_query, search_name, limit=35))
            if len({candidate.title for candidate in candidates}) >= 2:
                break

    unique = list({candidate.title: candidate for candidate in candidates}.values())
    if len(unique) < 2:
        raise RuntimeError(f"No two compatible skyline assets found for {city.ascii_name}.")

    winter = next(
        (candidate for candidate in unique if "winter" in f"{candidate.title} {candidate.description}".lower()),
        unique[0],
    )
    summer = next(
        (candidate for candidate in unique
         if candidate.title != winter.title
         and "summer" in f"{candidate.title} {candidate.description}".lower()),
        next(candidate for candidate in unique if candidate.title != winter.title),
    )
    return summer, winter


def write_webp(asset: CommonsAsset, destination: Path) -> str:
    image_data = request_bytes(asset.download_url)
    with Image.open(io.BytesIO(image_data)) as image:
        image = ImageOps.exif_transpose(image).convert("RGB")
        image = ImageOps.fit(image, (640, 360), method=Image.Resampling.LANCZOS, centering=(0.5, 0.48))
        image.save(destination, "WEBP", quality=78, method=6)
    return hashlib.sha256(destination.read_bytes()).hexdigest()


def prepare_city(city: City, output_dir: Path) -> PreparedCity:
    summer, winter = find_assets(city)
    prepared: list[tuple[str, CommonsAsset, str, Path, str]] = []
    temporary_paths: list[Path] = []
    try:
        for season, asset in (("summer", summer), ("winter", winter)):
            relative_path = f"Images/{city.slug}-{season}.webp"
            destination = output_dir / relative_path
            temporary = destination.with_suffix(".tmp.webp")
            sha256 = write_webp(asset, temporary)
            prepared.append((season, asset, relative_path, temporary, sha256))
            temporary_paths.append(temporary)
    except BaseException:
        for temporary in temporary_paths:
            temporary.unlink(missing_ok=True)
        raise
    return PreparedCity(city, tuple(prepared))


def create_database(path: Path) -> sqlite3.Connection:
    connection = sqlite3.connect(path)
    connection.executescript("""
        PRAGMA journal_mode = DELETE;
        PRAGMA foreign_keys = ON;
        CREATE TABLE catalog_metadata (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        ) STRICT;
        CREATE TABLE city (
            id TEXT PRIMARY KEY,
            geoname_id INTEGER NOT NULL UNIQUE,
            name TEXT NOT NULL,
            country_code TEXT NOT NULL,
            country_name TEXT NOT NULL,
            latitude REAL NOT NULL CHECK(latitude BETWEEN -90 AND 90),
            longitude REAL NOT NULL CHECK(longitude BETWEEN -180 AND 180),
            population INTEGER NOT NULL CHECK(population >= 0),
            timezone_id TEXT NOT NULL,
            is_capital INTEGER NOT NULL CHECK(is_capital IN (0, 1)),
            hemisphere TEXT NOT NULL CHECK(hemisphere IN ('north', 'south', 'equatorial'))
        ) STRICT;
        CREATE TABLE skyline_asset (
            city_id TEXT NOT NULL REFERENCES city(id) ON DELETE CASCADE,
            season TEXT NOT NULL CHECK(season IN ('summer', 'winter')),
            relative_path TEXT NOT NULL UNIQUE,
            title TEXT NOT NULL,
            author TEXT NOT NULL,
            source_url TEXT NOT NULL,
            download_url TEXT NOT NULL,
            license_name TEXT NOT NULL,
            license_url TEXT NOT NULL,
            description TEXT NOT NULL,
            source_width INTEGER NOT NULL,
            source_height INTEGER NOT NULL,
            sha256 TEXT NOT NULL,
            PRIMARY KEY (city_id, season)
        ) STRICT;
    """)
    connection.executemany("INSERT INTO catalog_metadata(key, value) VALUES(?, ?)", [
        ("schema_version", "1"),
        ("capital_count", str(CAPITAL_COUNT)),
        ("capital_selection", "Top 100 GeoNames PPLC records by population, then ASCII name"),
        ("city_count", str(CAPITAL_COUNT + 1)),
        ("city_source", "GeoNames cities15000"),
        ("city_source_url", GEONAMES_URL),
        ("city_source_license", "CC BY 4.0"),
        ("city_source_license_url", "https://creativecommons.org/licenses/by/4.0/"),
        ("image_source", "Wikimedia Commons"),
        ("image_transform", "Center-cropped and resized to 640x360 WebP by TrackMeUp"),
    ])
    return connection


def build(output_dir: Path, cache_dir: Path, fresh: bool, refresh_city_ids: list[str]) -> None:
    cache_dir.mkdir(parents=True, exist_ok=True)
    if fresh and output_dir.exists():
        expected_parent = (Path.cwd() / "TrackMeUp" / "Assets").resolve()
        if output_dir.parent != expected_parent or output_dir.name != "WorldClocks":
            raise RuntimeError(f"Refusing to replace unexpected output directory: {output_dir}")
        shutil.rmtree(output_dir)
    image_dir = output_dir / "Images"
    image_dir.mkdir(parents=True, exist_ok=True)
    cities = download_cities(cache_dir)
    database_path = output_dir / "world-clocks.sqlite3"
    connection = sqlite3.connect(database_path) if database_path.exists() else create_database(database_path)
    connection.execute(
        "INSERT OR REPLACE INTO catalog_metadata(key, value) VALUES(?, ?)",
        ("image_transform", "Center-cropped and resized to 640x360 WebP by TrackMeUp"),
    )
    connection.execute(
        "INSERT OR REPLACE INTO catalog_metadata(key, value) VALUES(?, ?)",
        ("capital_selection", "Top 100 GeoNames PPLC records by population, then ASCII name"),
    )
    connection.commit()
    known_city_ids = {city.slug for city in cities}
    unknown_refresh_ids = set(refresh_city_ids) - known_city_ids
    if unknown_refresh_ids:
        raise RuntimeError(f"Unknown city IDs requested for refresh: {sorted(unknown_refresh_ids)}")
    completed_city_ids = {
        row[0]
        for row in connection.execute(
            "SELECT city_id FROM skyline_asset GROUP BY city_id HAVING COUNT(*) = 2"
        )
    }
    manifest: dict = {
        "schemaVersion": 1,
        "cityData": {
            "source": "GeoNames cities15000",
            "url": GEONAMES_URL,
            "license": "CC BY 4.0",
            "licenseUrl": "https://creativecommons.org/licenses/by/4.0/",
        },
        "assets": [],
        "transformation": "Center-cropped and resized to 640x360 WebP by TrackMeUp",
    }
    try:
        refresh_city_id_set = set(refresh_city_ids)
        remaining = [
            city for city in cities
            if city.slug not in completed_city_ids or city.slug in refresh_city_id_set
        ]
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
            prepared_cities = executor.map(lambda city: prepare_city(city, output_dir), remaining)
            for completed_index, prepared_city in enumerate(prepared_cities, start=1):
                city = prepared_city.city
                absolute_index = cities.index(city) + 1
                print(f"[{absolute_index:03}/{len(cities)}] {city.ascii_name}", flush=True)
                hemisphere = "equatorial" if abs(city.latitude) < 12 else ("north" if city.latitude > 0 else "south")
                for _, _, relative_path, temporary_path, _ in prepared_city.assets:
                    temporary_path.replace(output_dir / relative_path)
                connection.execute(
                    """INSERT INTO city VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                       ON CONFLICT(id) DO UPDATE SET
                           geoname_id=excluded.geoname_id,
                           name=excluded.name,
                           country_code=excluded.country_code,
                           country_name=excluded.country_name,
                           latitude=excluded.latitude,
                           longitude=excluded.longitude,
                           population=excluded.population,
                           timezone_id=excluded.timezone_id,
                           is_capital=excluded.is_capital,
                           hemisphere=excluded.hemisphere""",
                    (city.slug, city.geoname_id, city.name, city.country_code, city.country_name, city.latitude, city.longitude,
                     city.population, city.timezone_id, int(city.is_capital), hemisphere),
                )
                connection.execute("DELETE FROM skyline_asset WHERE city_id = ?", (city.slug,))
                for season, asset, relative_path, _, sha256 in prepared_city.assets:
                    values = (
                        city.slug, season, relative_path, asset.title, asset.author, asset.source_url,
                        asset.download_url, asset.license_name, asset.license_url, asset.description,
                        asset.width, asset.height, sha256,
                    )
                    connection.execute("INSERT INTO skyline_asset VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", values)
                    manifest["assets"].append({
                        "cityId": city.slug,
                        "city": city.name,
                        "season": season,
                        "relativePath": relative_path,
                        "title": asset.title,
                        "author": asset.author,
                        "sourceUrl": asset.source_url,
                        "license": asset.license_name,
                        "licenseUrl": asset.license_url,
                        "sha256": sha256,
                    })
                connection.commit()
    finally:
        connection.close()
        for temporary_path in image_dir.glob("*.tmp.webp"):
            temporary_path.unlink(missing_ok=True)

    connection = sqlite3.connect(database_path)
    connection.row_factory = sqlite3.Row
    manifest["assets"] = [dict(row) for row in connection.execute("""
        SELECT a.city_id AS cityId, c.name AS city, a.season, a.relative_path AS relativePath,
               a.title, a.author, a.source_url AS sourceUrl, a.license_name AS license,
               a.license_url AS licenseUrl, a.sha256
        FROM skyline_asset a JOIN city c ON c.id = a.city_id
        ORDER BY c.name, a.season
    """)]
    connection.close()
    referenced_files = {output_dir / asset["relativePath"] for asset in manifest["assets"]}
    for image_path in image_dir.glob("*.webp"):
        if image_path not in referenced_files:
            image_path.unlink()
    (output_dir / "ATTRIBUTION.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    lines = [
        "# World clock data and skyline attribution",
        "",
        "City coordinates, population, and IANA time zones are derived from GeoNames `cities15000`,",
        "licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).",
        "",
        "Skyline images are resized/cropped copies from Wikimedia Commons. The exact author, source,",
        "license, and checksum for every distributed file are recorded below and in `ATTRIBUTION.json`.",
        "Each distributed WebP is a center-cropped 640×360 derivative and remains under its source license.",
        "",
        "| City | Season | Image | Author | License |",
        "|---|---|---|---|---|",
    ]
    for asset in manifest["assets"]:
        author = str(asset["author"]).replace("|", "\\|")
        title = str(asset["title"]).replace("|", "\\|")
        lines.append(
            f'| {asset["city"]} | {asset["season"]} | [{title}]({asset["sourceUrl"]}) | '
            f'{author} | [{asset["license"]}]({asset["licenseUrl"] or asset["sourceUrl"]}) |'
        )
    (output_dir / "ATTRIBUTION.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=Path("TrackMeUp/Assets/WorldClocks"))
    parser.add_argument("--cache", type=Path, default=Path(".cache/world-clock-catalog"))
    parser.add_argument("--fresh", action="store_true", help="Replace the validated product asset directory before building.")
    parser.add_argument("--refresh-city", action="append", default=[], help="Rebuild one existing city ID after visual QA.")
    args = parser.parse_args()
    build(args.output.resolve(), args.cache.resolve(), args.fresh, args.refresh_city)


if __name__ == "__main__":
    main()
