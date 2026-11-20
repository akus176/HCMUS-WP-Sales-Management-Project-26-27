#!/usr/bin/env python3
"""Verify the complete Unsplash image import against a new disposable storage volume."""
import hashlib
import json
import pathlib
import re
import secrets
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]


def main():
    project = "coffeeshop-photo-import-" + secrets.token_hex(6)
    manifest = json.loads((ROOT / "scripts/storage/unsplash-menu-photos.json").read_text(encoding="utf-8"))
    keys = {photo["key"] for photo in manifest}
    source = (ROOT / "src/CoffeeShop.WinUI/Services/MenuPhotoSource.cs").read_text(encoding="utf-8")
    required = {file + ".jpg" for file in re.findall(r'\["[^"]+"\] = "([^"]+)"', source)}
    required.add("cafe-bar.jpg")
    for page in (ROOT / "docs/ui-concepts").rglob("*.html"):
        required.update(re.findall(r'menu-images/([^"?\s]+\.jpg)', page.read_text(encoding="utf-8")))
    assert required <= keys, f"Missing downloadable photo sources: {required - keys}"
    with tempfile.TemporaryDirectory(prefix="coffeeshop-photo-import-check-") as temporary:
        env_file = pathlib.Path(temporary) / "storage.env"
        env_file.write_text("\n".join([
            "POSTGRES_DB=unused", "POSTGRES_USER=unused", "POSTGRES_PASSWORD=" + secrets.token_hex(24),
            "Jwt__Issuer=PhotoImportCheck", "Jwt__Audience=PhotoImportCheck", "Jwt__SigningKey=" + secrets.token_hex(48),
            "STORAGE_ACCESS_KEY=photo-import-check", "STORAGE_SECRET_KEY=" + secrets.token_hex(24),
            "STORAGE_PORT=0", "STORAGE_CONSOLE_PORT=0", "",
        ]), encoding="utf-8")
        env_file.chmod(0o600)
        compose = ["docker", "compose", "--project-name", project, "--env-file", str(env_file)]

        def run(*args, **kwargs):
            subprocess.run(compose + list(args), cwd=ROOT, check=True, **kwargs)

        def storage_url(path):
            address = subprocess.check_output(compose + ["port", "storage", "9000"], cwd=ROOT, text=True).strip()
            return f"http://{address}/{path}"

        def stored_digests():
            address = subprocess.check_output(compose + ["port", "storage", "9000"], cwd=ROOT, text=True).strip()
            digests = {}
            for key in sorted(keys):
                with urllib.request.urlopen(f"http://{address}/menu-images/{key}", timeout=20) as response:
                    assert response.headers.get_content_type() == "image/jpeg", key
                    data = response.read()
                    assert data.startswith(b"\xff\xd8\xff"), key
                    digests[key] = hashlib.sha256(data).hexdigest()
            return digests

        def expect_anonymous_denied(method, path, data=None):
            request = urllib.request.Request(storage_url(path), method=method, data=data)
            try:
                with urllib.request.urlopen(request, timeout=20):
                    raise AssertionError(f"Anonymous {method} unexpectedly succeeded: {path}")
            except urllib.error.HTTPError as error:
                assert error.code == 403, (method, path, error.code)

        probe_directory = pathlib.Path(temporary) / "probe"
        probe_directory.mkdir()
        original = b"Storage verification original\n"
        replacement = b"Storage verification replacement\n"
        (probe_directory / "original.txt").write_bytes(original)
        (probe_directory / "replacement.txt").write_bytes(replacement)

        def upload_probe(filename):
            run("run", "--rm", "--no-deps", "--volume", f"{probe_directory}:/probe:ro",
                "--entrypoint", "aws", "storage-init", "--endpoint-url", "http://storage:9000",
                "s3api", "put-object", "--bucket", "menu-images", "--key", "storage-check.txt",
                "--body", f"/probe/{filename}", "--content-type", "text/plain", stdout=subprocess.DEVNULL)

        def verify_probe(expected):
            with urllib.request.urlopen(storage_url("menu-images/storage-check.txt"), timeout=20) as response:
                assert response.headers.get_content_type() == "text/plain"
                assert response.read() == expected

        try:
            run("up", "--detach", "--wait", "--wait-timeout", "180", "storage")
            run("run", "--rm", "storage-init")
            subprocess.run([
                sys.executable, str(ROOT / "scripts/storage/import-unsplash-photos.py"),
                "--project-name", project, "--env-file", str(env_file),
            ], cwd=ROOT, check=True)
            before = stored_digests()
            expect_anonymous_denied("GET", "menu-images")
            expect_anonymous_denied("PUT", "menu-images/latte.jpg", b"denied")
            expect_anonymous_denied("DELETE", "menu-images/latte.jpg")
            assert stored_digests() == before, "Rejected anonymous requests altered the photos."
            upload_probe("original.txt")
            verify_probe(original)
            upload_probe("replacement.txt")
            run("run", "--rm", "storage-init")
            verify_probe(replacement)
            run("up", "--detach", "--wait", "--wait-timeout", "180", "--force-recreate", "storage")
            assert stored_digests() == before, "Photos changed or disappeared after container recreation."
            verify_probe(replacement)
            print(f"Storage check passed: all {len(keys)} photos restored; anonymous writes and listing denied; replacements preserved; bytes retained after recreation.")
        finally:
            run("down", "--volumes", "--remove-orphans")


if __name__ == "__main__":
    main()
