#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
project="coffeeshop-ci-$$"
env_file="$(mktemp)"
chmod 600 "$env_file"
cleanup() {
    docker compose --project-name "$project" --env-file "$env_file" down --volumes --remove-orphans
    rm -- "$env_file"
}
trap cleanup EXIT

{
    printf 'POSTGRES_DB=coffeeshop\nPOSTGRES_USER=coffeeshop\n'
    printf 'POSTGRES_PASSWORD=%s\n' "$(openssl rand -hex 24)"
    printf 'Jwt__Issuer=CoffeeShop.CI\nJwt__Audience=CoffeeShop.CI\n'
    printf 'Jwt__SigningKey=%s\n' "$(openssl rand -hex 48)"
    printf 'API_PORT=0\nPOSTGRES_PORT=0\n'
} > "$env_file"

compose() {
    docker compose --project-name "$project" --env-file "$env_file" "$@"
}

compose up --build --detach --wait --wait-timeout 180
compose ps
api_port="$(compose port api 8080)"
api_port="${api_port##*:}"
test "$(curl --silent --show-error "http://127.0.0.1:$api_port/health")" = Healthy
test "$(curl --silent --show-error "http://127.0.0.1:$api_port/health/ready")" = Healthy
migrations="$(compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc "SELECT count(*) FROM \"__EFMigrationsHistory\""')"
[[ "$migrations" =~ ^[0-9]+$ ]]
(( migrations > 0 ))
printf 'Compose healthy; %s database migrations applied.\n' "$migrations"
