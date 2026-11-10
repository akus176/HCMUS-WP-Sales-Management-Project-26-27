# Windows local startup verification (#195)

The setup change is in [PR #197](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/197). It maps to tasks 13.1, 13.2, and 13.6 on [OpenSpec #26](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/26).

## Recorded results

Verified on 10 November 2026 using setup commit `5ceb12d` and WinUI startup fix `c0b41df`.

| Check | Actual result |
| --- | --- |
| `bash scripts/verify-compose.sh` from a GitHub Actions checkout with an empty named volume | PostgreSQL and API healthy. `/health` and `/health/ready` both returned `Healthy`. PostgreSQL reported 10 applied migrations. |
| `docker compose run --rm demo-data` twice on that volume | Both runs produced the same counts shown below. The orders table was empty before the opt-in seed. |
| `docker compose exec -T api dotnet CoffeeShop.Api.dll --seed-staff-roles` | Six role bundles in PostgreSQL. |
| PowerShell configuration snippet on Windows against a clean clone, with Docker through WSL | Generated `.env` from the example. `git check-ignore -- .env` identified it as ignored. `docker compose config --quiet` succeeded. |
| `dotnet build src/CoffeeShop.WinUI -c Release -r win-x64 -p:WindowsAppSDKSelfContained=true` on Windows | Built from freshly extracted repository source with .NET SDK 10.0.403. Zero warnings and errors. |
| `dotnet test tests/CoffeeShop.WinUI.Tests -c Release --filter FullyQualifiedName~ApiSessionTests` on Windows | 17 passed, zero failed or skipped. These tests verify the client session code. |
| Release WinUI in `direct` mode against a disposable PostgreSQL 16.15 database on Windows | Used the restricted `coffeeshop_midterm` role. The native window showed `WINUI · DIRECT POSTGRESQL`, `LOCAL · CONNECTED`, and `20 items · 20 active` in Menu. |
| Windows API and account check against the same disposable database | `--provision-staff` created a Manager. `/health` and `/health/ready` returned HTTP 200 and `Healthy`. `/api/v1/auth/login` and `/api/v1/auth/me` confirmed the Manager role. An authenticated category read returned five rows. |
| README Release launch command with `COFFEESHOP_CLIENT_MODE=api` | The native `Corner Cafe · Sign in` window opened. Credentials were submitted only in the separate HTTP check above. |
| `openspec validate build-coffee-shop-management-system --strict` | Passed. |

The [Compose job log](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/42813934365/job/129822083874) records the health and database checks. The automated check removed its containers and test volume afterward.

The issue owner confirmed delivery of the populated `.env` through the team's restricted private channel. The [delivery checkbox on issue #195](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/195) records that confirmation.

| Categories | Products | Sizes | Recipes | Ingredients | Suppliers | Receipts | Customers | Tables | Orders |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 5 | 20 | 40 | 40 | 9 | 3 | 2 | 12 | 8 | 24 |

## Remaining review evidence

- Run the complete [Windows quickstart](../../README.md#start-the-api-and-winui-on-windows) in one clean Windows checkout. Record the Compose service health, both HTTP health responses, and the Release WinUI Menu after signing in with the disposable Manager. Keep credentials and tokens out of the evidence. The clean Compose check ran on GitHub Actions, and the recorded Windows checks used a separately hosted API and PostgreSQL database.
- Record reviewer approval after the clean-checkout walkthrough.
