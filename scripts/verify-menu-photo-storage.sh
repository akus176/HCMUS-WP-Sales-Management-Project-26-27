#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

project="coffeeshop-photos-check-$$"
probe_dir="$(mktemp -d)"
env_file="$probe_dir/storage.env"
touch "$env_file"
chmod 600 "$env_file"
compose() {
    docker compose --project-name "$project" --env-file "$env_file" "$@"
}
cleanup() {
    compose down --volumes --remove-orphans
    rm -- "$env_file" "$probe_dir/original.txt" "$probe_dir/replacement.txt"
    rmdir -- "$probe_dir"
}
trap cleanup EXIT
printf 'Storage verification original\n' > "$probe_dir/original.txt"
printf 'Storage verification replacement\n' > "$probe_dir/replacement.txt"
{
    printf 'POSTGRES_DB=unused\nPOSTGRES_USER=unused\nPOSTGRES_PASSWORD=%s\n' "$(openssl rand -hex 24)"
    printf 'Jwt__Issuer=PhotoCheck\nJwt__Audience=PhotoCheck\nJwt__SigningKey=%s\n' "$(openssl rand -hex 48)"
    printf 'STORAGE_ACCESS_KEY=photo-check\nSTORAGE_SECRET_KEY=%s\n' "$(openssl rand -hex 24)"
    printf 'STORAGE_PORT=0\nSTORAGE_CONSOLE_PORT=0\n'
} > "$env_file"

compose up --detach --wait --wait-timeout 180 storage
compose run --rm storage-init
address="$(compose port storage 9000)"
upload_probe() {
    compose run --rm --volume "$probe_dir:/probe:ro" --entrypoint aws storage-init \
        --endpoint-url http://storage:9000 s3 cp "/probe/$1" s3://menu-images/storage-check.txt \
        --content-type text/plain --only-show-errors
}
upload_probe original.txt
curl --fail --silent --show-error "http://$address/menu-images/storage-check.txt" | cmp "$probe_dir/original.txt" -
curl --fail --silent --head "http://$address/menu-images/storage-check.txt" | tr -d '\r' | grep -iq '^content-type: text/plain$'

# Only known-object reads are anonymous; rejected writes must not alter a picture.
test "$(curl --silent --output /dev/null --write-out '%{http_code}' "http://$address/menu-images")" = 403
test "$(curl --silent --output /dev/null --write-out '%{http_code}' -X PUT --data 'denied' "http://$address/menu-images/storage-check.txt")" = 403
test "$(curl --silent --output /dev/null --write-out '%{http_code}' -X DELETE "http://$address/menu-images/storage-check.txt")" = 403
curl --fail --silent --show-error "http://$address/menu-images/storage-check.txt" | cmp "$probe_dir/original.txt" -

# Simulate a manager replacing a picture; initialization must retain the replacement.
upload_probe replacement.txt
compose run --rm storage-init
curl --fail --silent --show-error "http://$address/menu-images/storage-check.txt" | cmp "$probe_dir/replacement.txt" -

# Recreate the server against the same named volume to verify durable storage.
compose up --detach --wait --wait-timeout 180 --force-recreate storage
address="$(compose port storage 9000)"
curl --fail --silent --show-error "http://$address/menu-images/storage-check.txt" | cmp "$probe_dir/replacement.txt" -
echo 'Photo storage passed: downloaded bytes match, anonymous writes denied, replacements and volume persistence retained.'
