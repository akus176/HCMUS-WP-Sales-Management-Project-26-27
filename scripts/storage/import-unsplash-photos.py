#!/usr/bin/env python3
"""Download the selected Unsplash photographs into RustFS, never into the checkout."""
import argparse
import concurrent.futures
import hashlib
import json
import pathlib
import subprocess
import tempfile
import urllib.parse
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[2]
MANIFEST = pathlib.Path(__file__).with_name("unsplash-menu-photos.json")


def download(photo, directory):
    url = photo["image_url"] + "?" + urllib.parse.urlencode(
        {"fm": "jpg", "fit": "max", "w": 1200, "q": 85}
    )
    request = urllib.request.Request(url, headers={"User-Agent": "CoffeeShop-PhotoImport/1.0"})
    with urllib.request.urlopen(request, timeout=60) as response:
        data = response.read(10 * 1024 * 1024 + 1)
        if response.headers.get_content_type() != "image/jpeg" or not data.startswith(b"\xff\xd8\xff"):
            raise ValueError(f"Not a JPEG: {photo['key']}")
    if len(data) > 10 * 1024 * 1024:
        raise ValueError(f"Photo exceeds 10 MiB: {photo['key']}")
    path = directory / photo["key"]
    path.write_bytes(data)
    return photo, hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--download-only", action="store_true", help="Validate downloads without uploading.")
    parser.add_argument("--preview-dir", type=pathlib.Path, help="Keep review copies outside the repository.")
    parser.add_argument("--project-name", help="Use a separate Compose project, for example for a clean-storage check.")
    parser.add_argument("--env-file", type=pathlib.Path, help="Use an alternate Compose environment file.")
    args = parser.parse_args()
    compose = ["docker", "compose"]
    if args.project_name:
        compose += ["--project-name", args.project_name]
    if args.env_file:
        compose += ["--env-file", str(args.env_file.resolve())]
    if args.preview_dir:
        directory = args.preview_dir.resolve()
        if directory.is_relative_to(ROOT):
            parser.error("Review pictures must stay outside the repository.")
        directory.mkdir(parents=True, exist_ok=True)
        args.preview_dir = directory
    photos = json.loads(MANIFEST.read_text(encoding="utf-8"))
    if len({photo["key"] for photo in photos}) != len(photos):
        raise ValueError("Each photo must have a unique storage key.")
    for photo in photos:
        source = urllib.parse.urlparse(photo["image_url"])
        if source.scheme != "https" or source.hostname != "images.unsplash.com":
            raise ValueError("Only selected Unsplash sources are supported.")
        if pathlib.Path(photo["key"]).name != photo["key"] or not photo["key"].endswith(".jpg"):
            raise ValueError("Invalid storage key.")
    with tempfile.TemporaryDirectory(prefix="coffeeshop-unsplash-") as temporary:
        directory = pathlib.Path(temporary)
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            results = list(pool.map(lambda photo: download(photo, directory), photos))
        if args.preview_dir:
            for photo, _ in results:
                (args.preview_dir / photo["key"]).write_bytes((directory / photo["key"]).read_bytes())
        if not args.download_only:
            address = published_address(compose)
            for photo, digest in results:
                metadata = json.dumps({"source": "unsplash", "photo-id": photo["photo_id"], "sha256": digest})
                subprocess.run(compose + [
                    "run", "--rm", "--no-deps", "--volume", f"{directory}:/uploads:ro",
                    "--entrypoint", "aws", "storage-init", "--endpoint-url", "http://storage:9000",
                    "s3api", "put-object", "--bucket", "menu-images", "--key", photo["key"],
                    "--body", f"/uploads/{photo['key']}", "--content-type", "image/jpeg",
                    "--cache-control", "public, max-age=300", "--metadata", metadata,
                ], cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
                with urllib.request.urlopen(f"http://{address}/menu-images/{photo['key']}", timeout=20) as response:
                    if hashlib.sha256(response.read()).hexdigest() != digest:
                        raise ValueError(f"Stored photo differs: {photo['key']}")
        print(f"{'Validated' if args.download_only else 'Stored and verified'} {len(results)} Unsplash photographs.")


def published_address(compose):
    return subprocess.check_output(
        compose + ["port", "storage", "9000"], cwd=ROOT, text=True
    ).strip()


if __name__ == "__main__":
    main()
