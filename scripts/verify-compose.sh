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
    printf 'STORAGE_ACCESS_KEY=coffeeshop-ci\nSTORAGE_SECRET_KEY=%s\n' "$(openssl rand -hex 24)"
    printf 'STORAGE_PORT=0\nSTORAGE_CONSOLE_PORT=0\n'
} > "$env_file"

compose() {
    docker compose --project-name "$project" --env-file "$env_file" "$@"
}

compose up --build --detach --wait --wait-timeout 180
compose ps
photo_port="$(compose port storage 9000)"
# Anonymous storage access is limited to reading known objects.
test "$(curl --silent --output /dev/null --write-out '%{http_code}' "http://$photo_port/menu-images")" = 403
test "$(curl --silent --output /dev/null --write-out '%{http_code}' -X PUT --data 'denied' "http://$photo_port/menu-images/forbidden.jpg")" = 403
test "$(curl --silent --output /dev/null --write-out '%{http_code}' -X DELETE "http://$photo_port/menu-images/latte.jpg")" = 403
# Initialization also succeeds on an existing bucket without source pictures.
compose run --rm storage-init
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

# Provision disposable accounts without placing passwords in arguments or logs.
export ProvisionStaff__Password
ProvisionStaff__Password="$(openssl rand -hex 24)"
for role in Manager Cashier; do
    compose exec -T -e ProvisionStaff__Password \
        -e "ProvisionStaff__Username=collection-$role" \
        -e "ProvisionStaff__Email=collection-$role@example.test" \
        -e "ProvisionStaff__FullName=Collection $role" \
        -e "ProvisionStaff__Roles=$role" api dotnet CoffeeShop.Api.dll --provision-staff
done
export COFFEESHOP_COLLECTION_API_HOST="http://127.0.0.1:$api_port"
export COFFEESHOP_COLLECTION_MANAGER_USERNAME=collection-Manager
export COFFEESHOP_COLLECTION_CASHIER_USERNAME=collection-Cashier
export COFFEESHOP_COLLECTION_MANAGER_PASSWORD="$ProvisionStaff__Password"
export COFFEESHOP_COLLECTION_CASHIER_PASSWORD="$ProvisionStaff__Password"
mkdir -p TestResults
node tests/api/run-collection.mjs TestResults/api-collection.json
# A deliberately wrong password must fail, while the second account still works.
if COFFEESHOP_COLLECTION_MANAGER_PASSWORD=deliberately-invalid \
    node tests/api/run-collection.mjs TestResults/api-collection-negative.json; then
    echo 'Collection incorrectly accepted invalid credentials.' >&2
    exit 1
fi
node -e 'const r = require("./TestResults/api-collection-negative.json");
    if (r.passed || r.requests.find(x => x.name === "Manager login")?.status !== 401 ||
        r.requests.find(x => x.name === "Cashier login")?.status !== 200) process.exit(1);'
test "$(query 'SELECT count(*) FROM catalog.categories')" = 5
echo 'API collection passed; invalid credentials rejected without catalog changes.'
