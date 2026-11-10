#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
contract_dir="$repo_root/contracts/mobile-p0"
dotnet_bin=${DOTNET_BIN:-dotnet}
contract_port=${MOBILE_CONTRACT_PORT:-5137}
server_log=$(mktemp)
response_file=$(mktemp)
cleanup() {
  if [[ -n "${server_pid:-}" ]]; then
    kill "$server_pid" 2>/dev/null || true
    wait "$server_pid" 2>/dev/null || true
  fi
  rm -f "$server_log" "$response_file"
}
trap cleanup EXIT

ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="http://127.0.0.1:$contract_port" \
ConnectionStrings__CoffeeShop='Host=127.0.0.1;Port=1;Database=contract;Username=contract;Timeout=1' \
Jwt__SigningKey="$(openssl rand -hex 32)" \
Jwt__Issuer=contract-test Jwt__Audience=contract-test \
"$dotnet_bin" "$repo_root/src/CoffeeShop.Api/bin/Release/net10.0/CoffeeShop.Api.dll" \
  >"$server_log" 2>&1 &
server_pid=$!

for attempt in {1..80}; do
  if curl --silent --show-error --fail "http://127.0.0.1:$contract_port/openapi/v1.json" \
    --output "$response_file" 2>/dev/null; then
    break
  fi
  if ! kill -0 "$server_pid" 2>/dev/null; then
    cat "$server_log" >&2
    exit 1
  fi
  sleep 0.25
done
if [[ ! -s "$response_file" ]]; then
  cat "$server_log" >&2
  exit 1
fi

node -e 'const fs = require("node:fs"); const sort = value => Array.isArray(value) ? value.map(sort) : value && typeof value === "object" ? Object.fromEntries(Object.keys(value).sort().map(key => [key, sort(value[key])])) : value; const doc = JSON.parse(fs.readFileSync(process.argv[1], "utf8")); fs.writeFileSync(process.argv[2], JSON.stringify(sort(doc), null, 2) + "\n");' \
  "$response_file" "$contract_dir/openapi.json"
(
  cd "$contract_dir"
  node node_modules/openapi-typescript/bin/cli.js openapi.json --output schema.d.ts
)
