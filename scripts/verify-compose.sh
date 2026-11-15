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

# Use generated synthetic credentials without putting their values in process
# arguments or reports. Postman CLI executes the committed collection unchanged.
export COFFEESHOP_COLLECTION_API_HOST=http://api:8080
export COFFEESHOP_COLLECTION_MANAGER_USERNAME=collection_manager
export COFFEESHOP_COLLECTION_CASHIER_USERNAME=collection_cashier
export COFFEESHOP_COLLECTION_MANAGER_PASSWORD="$(openssl rand -hex 24)"
export COFFEESHOP_COLLECTION_CASHIER_PASSWORD="$(openssl rand -hex 24)"
for role in Manager Cashier; do
    export ProvisionStaff__Username ProvisionStaff__Password ProvisionStaff__Email ProvisionStaff__FullName ProvisionStaff__Roles
    if [[ "$role" == Manager ]]; then
        ProvisionStaff__Username="$COFFEESHOP_COLLECTION_MANAGER_USERNAME"
        ProvisionStaff__Password="$COFFEESHOP_COLLECTION_MANAGER_PASSWORD"
    else
        ProvisionStaff__Username="$COFFEESHOP_COLLECTION_CASHIER_USERNAME"
        ProvisionStaff__Password="$COFFEESHOP_COLLECTION_CASHIER_PASSWORD"
    fi
    ProvisionStaff__Email="$ProvisionStaff__Username@example.test"
    ProvisionStaff__FullName="Collection $role"
    ProvisionStaff__Roles="$role"
    compose exec -T -e ProvisionStaff__Username -e ProvisionStaff__Password -e ProvisionStaff__Email \
        -e ProvisionStaff__FullName -e ProvisionStaff__Roles api dotnet CoffeeShop.Api.dll --provision-staff
done
unset ProvisionStaff__Username ProvisionStaff__Password ProvisionStaff__Email ProvisionStaff__FullName ProvisionStaff__Roles

results_dir="${COFFEESHOP_API_COLLECTION_RESULTS:-$(mktemp -d)}"
mkdir -p "$results_dir"
results_dir="$(cd "$results_dir" && pwd)"
docker run --rm --network "${project}_default" \
    --mount "type=bind,source=$PWD/tests/api,target=/source,readonly" \
    --mount "type=bind,source=$results_dir,target=/results" \
    -e COFFEESHOP_COLLECTION_API_HOST -e COFFEESHOP_COLLECTION_MANAGER_USERNAME \
    -e COFFEESHOP_COLLECTION_MANAGER_PASSWORD -e COFFEESHOP_COLLECTION_CASHIER_USERNAME \
    -e COFFEESHOP_COLLECTION_CASHIER_PASSWORD node:24-bookworm-slim sh -ec '
        mkdir /tmp/collection
        cp /source/*.json /source/*.mjs /tmp/collection/
        cd /tmp/collection
        npm ci --no-fund --no-audit --silent
        npm test
        node run-collection.mjs /results/collection.json
        if COFFEESHOP_COLLECTION_MANAGER_PASSWORD=intentionally-invalid node run-collection.mjs /results/negative-control.json; then
            printf "FAIL: invalid credentials unexpectedly passed.\n"
            exit 1
        fi
        printf "PASS: deliberately invalid credentials fail the collection.\n"
    '
printf 'Sanitized collection evidence: %s\n' "$results_dir"
