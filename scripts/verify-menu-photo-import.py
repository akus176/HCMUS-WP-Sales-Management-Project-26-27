#!/usr/bin/env python3
"""Verify the complete Unsplash image import against a new disposable storage volume."""
import hashlib
import json
import pathlib
import re
import secrets
import subprocess
import tempfile
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

        def run(*args):
            subprocess.run(compose + list(args), cwd=ROOT, check=True)

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

        try:
            run("up", "--detach", "--wait", "--wait-timeout", "180", "storage")
            run("run", "--rm", "storage-init")
            subprocess.run([
                "python3", str(ROOT / "scripts/storage/import-unsplash-photos.py"),
                "--project-name", project, "--env-file", str(env_file),
            ], cwd=ROOT, check=True)
            before = stored_digests()
            run("up", "--detach", "--wait", "--wait-timeout", "180", "--force-recreate", "storage")
            assert stored_digests() == before, "Photos changed or disappeared after container recreation."
            print(f"Clean storage import passed: all {len(keys)} photos restored; menu, fallback and design references covered; bytes retained after recreation.")
        finally:
            run("down", "--volumes", "--remove-orphans")


if __name__ == "__main__":
    main()
