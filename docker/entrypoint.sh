#!/usr/bin/env bash
set -euo pipefail

dotnet CoffeeShop.Api.dll --migrate
exec dotnet CoffeeShop.Api.dll
