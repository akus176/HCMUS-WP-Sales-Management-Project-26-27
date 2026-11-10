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
test "$(curl --fail --silent --show-error "http://127.0.0.1:$api_port/health")" = Healthy
test "$(curl --fail --silent --show-error "http://127.0.0.1:$api_port/health/ready")" = Healthy

query() {
    compose exec -T postgres sh -c 'exec psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc "$1"' sh "$1"
}

migrations="$(query 'SELECT count(*) FROM "__EFMigrationsHistory"')"
[[ "$migrations" =~ ^[0-9]+$ ]]
(( migrations > 0 ))
printf 'Compose healthy; %s database migrations applied.\n' "$migrations"

demo_counts() {
    query 'SELECT (SELECT count(*) FROM catalog.categories),
                  (SELECT count(*) FROM catalog.menu_items),
                  (SELECT count(*) FROM catalog.size_variants),
                  (SELECT count(*) FROM catalog.recipe_ingredients),
                  (SELECT count(*) FROM inventory.ingredients),
                  (SELECT count(*) FROM inventory.suppliers),
                  (SELECT count(*) FROM inventory.purchase_receipts),
                  (SELECT count(*) FROM customers.customers),
                  (SELECT count(*) FROM sales.tables),
                  (SELECT count(*) FROM sales.orders)'
}

test "$(query 'SELECT count(*) FROM sales.orders')" = 0
expected='5|20|40|40|9|3|2|12|8|24'
for run in 1 2; do
    compose run --rm demo-data
    counts="$(demo_counts)"
    printf 'Demo seed run %s (categories|products|sizes|recipes|ingredients|suppliers|receipts|customers|tables|orders): %s\n' "$run" "$counts"
    test "$counts" = "$expected"
done

compose exec -T api dotnet CoffeeShop.Api.dll --seed-staff-roles
roles="$(query 'SELECT count(*) FROM identity.roles')"
test "$roles" = 6
printf 'Staff role bundles: %s\n' "$roles"
