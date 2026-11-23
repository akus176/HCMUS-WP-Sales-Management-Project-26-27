#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
tools="$(mktemp -d)"
trap 'rm -rf -- "$tools"' EXIT
version=8.24.3
archive="gitleaks_${version}_linux_x64.tar.gz"
base="https://github.com/gitleaks/gitleaks/releases/download/v$version"
curl --fail --silent --show-error --location "$base/$archive" -o "$tools/$archive"
curl --fail --silent --show-error --location "$base/gitleaks_${version}_checksums.txt" -o "$tools/checksums.txt"
(cd "$tools" && grep "  $archive$" checksums.txt | sha256sum --check -)
tar -xzf "$tools/$archive" -C "$tools" gitleaks
# Construct a synthetic token in memory: this must fail or the scanner gate is broken.
set +e
printf 'github_token = "%s%s"\n' ghp_ "$(openssl rand -hex 18)" | "$tools/gitleaks" stdin --no-banner --redact >/dev/null 2>&1
status=$?
set -e
test "$status" = 1
echo 'Secret gate: synthetic credential rejected.'
"$tools/gitleaks" git --no-banner --redact --log-opts=HEAD .
