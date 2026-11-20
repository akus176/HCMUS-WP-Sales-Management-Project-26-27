#!/usr/bin/env bash
set -euo pipefail

usage() {
    printf '%s\n' 'Usage: ./scripts/run-winui-wsl.sh [--build-api]' \
        'Start the Docker backend in WSL and build and open WinUI on Windows.' \
        'Pass --build-api to rebuild the API image after changing backend code.'
}

compose_build=()
case "${1:-}" in
    '') ;;
    --build-api) compose_build=(--build) ;;
    --help|-h) usage; exit 0 ;;
    *) usage >&2; exit 2 ;;
esac
if (( $# > 1 )); then usage >&2; exit 2; fi

for tool in docker wslpath pwsh.exe rsync flock; do
    if ! command -v "$tool" >/dev/null; then
        printf 'Missing %s. Use Docker integration, PowerShell 7.4 or later on Windows, and rsync and flock in WSL.\n' "$tool" >&2
        exit 1
    fi
done

cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
checkout=$(pwd -P)
if [[ ! -f .env ]]; then
    printf '%s\n' 'Create .env using .env.example and the setup steps in README.md, then rerun the launcher.' >&2
    exit 1
fi

windows_appdata=$(pwsh.exe -NoProfile -NonInteractive -Command '[Console]::WriteLine($env:LOCALAPPDATA)' | tr -d '\r')
local_appdata=$(realpath -- "$(wslpath -u "$windows_appdata")")
checkout_id=$(printf '%s' "$checkout" | sha256sum | cut -c1-16)
stage="$local_appdata/CoffeeShop/WinUI-WSL/$checkout_id"
mkdir -p -- "$stage"
if [[ $(realpath -- "$stage") != "$stage" ]]; then
    printf '%s\n' 'The Windows build directory resolves outside its expected path.' >&2
    exit 1
fi

lock_dir="${XDG_CACHE_HOME:-$HOME/.cache}/CoffeeShop/WinUI-WSL"
mkdir -p -- "$lock_dir"
exec 9>"$lock_dir/$checkout_id.lock"
if ! flock -n 9; then
    printf '%s\n' 'The launcher is already running for this checkout.' >&2
    exit 1
fi

printf '%s\n' 'Starting the API, database, and photo storage with Docker Compose…'
docker compose up "${compose_build[@]}" --detach --wait --wait-timeout 180 api
published_address=$(docker compose port api 8080)
if [[ ! $published_address =~ ^127\.0\.0\.1:[0-9]+$ ]]; then
    printf '%s\n' 'Could not determine the loopback API port from Docker Compose.' >&2
    exit 1
fi
photo_address=$(docker compose port storage 9000)
if [[ ! $photo_address =~ ^127\.0\.0\.1:[0-9]+$ ]]; then
    printf '%s\n' 'Could not determine the loopback photo storage port from Docker Compose.' >&2
    exit 1
fi

# WinUI build tools require a Windows drive. Copy only the projects used by the Release client.
rsync --recursive --times --relative --delete \
    --exclude='bin/' --exclude='obj/' --exclude='.env*' --exclude='appsettings.Local.json' \
    global.json Directory.Build.props Directory.Packages.props scripts/run-winui.ps1 \
    src/CoffeeShop.WinUI/ src/CoffeeShop.Application/ src/CoffeeShop.Contracts/ src/CoffeeShop.Domain/ \
    "$stage/"

if pwsh.exe -NoProfile -NonInteractive -File "$(wslpath -w "$stage/scripts/run-winui.ps1")" \
    -WslApiUrl "http://$published_address"; then
    printf 'Opening Corner Cafe with the API at http://%s.\n' "$published_address"
else
    status=$?
    # The Windows build script uses 10 when this checkout's app is already open.
    if (( status == 10 )); then exit 0; fi
    exit "$status"
fi

cd -- "$stage/src/CoffeeShop.WinUI/bin/Release/net10.0-windows10.0.19041.0/win-x64"
# The GUI can outlive the WSL command and must not inherit the build lock.
COFFEESHOP_CLIENT_MODE=api COFFEESHOP_API_BASE_URL="http://$published_address/api/v1/" \
    COFFEESHOP_MENU_PHOTOS_BASE_URL="http://$photo_address/menu-images/" \
    WSLENV="${WSLENV:+$WSLENV:}COFFEESHOP_CLIENT_MODE/w:COFFEESHOP_API_BASE_URL/w:COFFEESHOP_MENU_PHOTOS_BASE_URL/w" \
    nohup ./CoffeeShop.WinUI.exe </dev/null >"$stage/winui.log" 2>&1 9>&- &
app_worker=$!

# Keep the interop session available until Windows has started the executable.
if ! COFFEESHOP_WSL_EXE="$(wslpath -w "$PWD/CoffeeShop.WinUI.exe")" \
    WSLENV="${WSLENV:+$WSLENV:}COFFEESHOP_WSL_EXE/w" \
    pwsh.exe -NoProfile -NonInteractive -Command '
        for ($attempt = 0; $attempt -lt 40; $attempt++) {
            if (Get-Process -Name CoffeeShop.WinUI -ErrorAction SilentlyContinue |
                Where-Object { $_.Path -eq $env:COFFEESHOP_WSL_EXE -and $_.MainWindowHandle -ne 0 }) {
                exit 0
            }
            Start-Sleep -Milliseconds 250
        }
        exit 1
    '; then
    printf 'WinUI did not open a window. Check %s/winui.log.\n' "$stage" >&2
    exit 1
fi
disown "$app_worker"
