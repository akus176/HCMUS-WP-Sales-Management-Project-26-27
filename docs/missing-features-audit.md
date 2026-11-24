# Missing Features Audit — Inconsistencies Between Layers

This report lists features that are **missing at one layer while another layer already depends on them**, producing inconsistency in the application.

Scope audited: API (`src/CoffeeShop.Api`), desktop app (`src/CoffeeShop.WinUI`), mobile app (`apps/staff-mobile`), domain/persistence (`src/CoffeeShop.Domain`, `src/CoffeeShop.Infrastructure.Postgres`), API contract (`contracts/mobile-p0/openapi.json`), and seed scripts (`scripts/postgres/`).

---

## Account model decision

To eliminate password-management work, the product adopts a two-tier identity model:

| Account tier | How it is created | How it signs in |
|---|---|---|
| **Administrator** | Linked to a **Google account** — no local password exists | Google sign-in (OAuth) |
| **Staff (mobile account)** | Created **only by an administrator in the Windows app** (name, role, initial password) | Username + password set by the administrator at creation |

Consequences: no password reset/change feature is needed for administrators (Google owns the credential), and staff passwords are set once by the administrator at creation time — a forgotten staff password is handled by the administrator deactivating and re-creating the account, not by a reset flow.

---

## In scope — fix this week

Only the gaps below break the product's own logic during a demo.

### 0. Google sign-in for administrator accounts
| | |
|---|---|
| **Exists today** | The API issues its own JWTs from a local username/password check (`POST /auth/login`, `StaffIdentityEndpoints.cs:82`). Every account — including administrators — depends on a locally hashed password provisioned via CLI. |
| **Missing** | Any Google/OAuth flow. Administrator accounts cannot be linked to a Google identity. |
| **Fix** | Add Google authentication to the API (validate a Google ID token with `Microsoft.AspNetCore.Authentication.JwtBearer` + Google's JWKS, or full OAuth via `AddGoogle()`), then link/accept the Google email as the administrator identity when issuing the app's own JWT. On WinUI, replace the password boxes in `LoginWindow` with an interactive browser flow (system browser or WebView2) that returns the Google result to the app. |
| **Risk note** | This is the largest new item of the week (OAuth client setup, redirect handling in a desktop app). If it threatens the deadline, the fallback is to keep CLI provisioning for the single administrator demo account — the model above still holds because *no staff-facing password feature* is implied either way. |

### 1. Create a staff account from the Windows app (administrator only)
| | |
|---|---|
| **Exists today** | Accounts can be listed, inspected, and deactivated by a `users.manage` administrator — on the API (`GET /users`, `GET /users/{id}`, `POST /users/{id}/deactivate` in `src/CoffeeShop.Api/StaffIdentityEndpoints.cs:106-131`) and in the mobile Administration module (`apps/staff-mobile/src/features/administration/StaffAdministration.tsx`). |
| **Missing** | **Create a staff account with a role** — the only way to make an account is the CLI provisioning command (`--provision-staff`, `src/CoffeeShop.Api/StaffProvisioning.cs`, run via `docker compose exec` per `README.md` step 3). Roles on an account are displayed read-only and can never be changed. Per the account model above, staff accounts are created **in the Windows app only**; the mobile app keeps its existing view/deactivate administration. |
| **Inconsistency** | An administrator can *deactivate* staff but cannot *hire* (create) them. Every new employee requires shell access to the server, which no business user has. |
| **Fix** | Add `POST /users` (username, email, full name, phone, role, initial password) to `StaffIdentityEndpoints.cs` with `users.manage` authorization, reusing the validation and password hashing from `StaffProvisioning.cs`. On the desktop, add a small **Accounts** surface (a new page or an admin-only dialog reachable from the shell) with a "New staff account" form — the WinUI app currently has no staff UI at all, so this is its first administration view. The administrator sets the initial password; no reset feature follows from the account model. |

### 2. Dining table creation
| | |
|---|---|
| **Exists today** | Tables appear throughout the system: the Counter has a table picker (`TableBox` in `src/CoffeeShop.WinUI/Pages/PosPage.xaml:428`), the Service board prints the table number on cards, and the mobile cashier has a read-only "Table occupancy" tab (`GET /api/v1/pos/tables`). |
| **Missing** | **Create a table** — the API exposes only `GET /tables` and `GET /pos/tables` (`src/CoffeeShop.Api/SalesEndpoints.cs:81-83`); there is no `POST/PUT/DELETE` anywhere. Every table row was inserted by `scripts/postgres/windows-demo-seed.sql:191`. |
| **Inconsistency** | A fresh/empty database has **zero tables**, so Dine-in ordering is unusable until someone edits SQL. If the shop adds a physical table, the system can never reflect it. |
| **Fix** | Add `POST /tables` (table number, seats) and optionally `PUT /tables/{id}` to `SalesEndpoints.cs` guarded by an appropriate permission. Surface it as a minimal manager action (e.g., an "Add table" button on the mobile Table occupancy tab or a desktop dialog) — no floor plan needed. |

### 3. Wire the missing stock-room edit actions (API already built)
| | |
|---|---|
| **Exists today** | The API is complete: `POST /ingredients/{id}/deactivate`, `/activate`, `/threshold` and `POST /suppliers/{id}/deactivate`, `/activate` (`src/CoffeeShop.Api/InventoryEndpoints.cs:59-127`). The desktop client methods exist too (`src/CoffeeShop.WinUI/Services/ApiInventorySource.cs:63-69`). |
| **Missing** | No button calls any of them. Verified: `SetThresholdAsync` and the four activate/deactivate methods have **zero call sites**. The `IngredientDialog` edit mode (threshold-only, `IngredientDialog.xaml.cs:26-35`) is **dead code** — the dialog is only ever constructed in create mode (`StockRoomPage.xaml.cs:568`), so an ingredient's threshold cannot be changed from any UI. The supplier Edit button, by contrast, works correctly in both modes (`StockRoomPage.xaml.cs:746-783`). |
| **Inconsistency** | The UI pretends ingredient editing exists (a full edit-capable dialog ships in the code) while the actions are unreachable. |
| **Fix** | In the Stock room: add an ingredient Edit action that opens `IngredientDialog(ingredient)` — the dialog already implements everything, it just needs a caller — plus Deactivate/Activate row actions for ingredients and suppliers, following the Catalog page's existing Deactivate pattern. |

---

## Historical detail — full audit

<details>
<summary>Complete gap inventory (kept for reference)</summary>

### API-level gaps with no client
- No `POST /users` (create), role assignment, password reset, or reactivation for staff accounts (`StaffIdentityEndpoints.cs:106-131`).
- No `POST/PUT/DELETE` for dining tables; only `GET /tables` and `GET /pos/tables` (`SalesEndpoints.cs:81-83`).
- No reservation endpoints at all despite full domain/persistence support (`Reservation.cs`, `ReservationConfiguration.cs`).
- Payment recording only — no refund or void (`SalesEndpoints.cs:46`).
- Roles/permissions are static seed bundles; no management endpoints (`StaffIdentityEndpoints.cs:73-77`).
- No activate/reactivate for categories, menu items, size variants, or addons — deactivate is one-way (`CatalogEndpoints.cs:53,75,82,129`, `Program.cs:167`).
- No customer/address hard delete or reactivate (`CustomerEndpoints.cs:52,104`).
- No ingredient update (rename/unit change) (`InventoryEndpoints.cs:39-100`).
- No supplier get-by-id; no hard delete for any resource.
- No purchase-receipt header edit or draft delete (`InventoryEndpoints.cs:134-196`).
- Recipe editing is full-replace only (`CatalogEndpoints.cs:103`).
- Mobile device registration/revocation is owner-only; no admin list or revoke (`MobileDeviceEndpoints.cs:18-49`).

### UI-level gaps
- Desktop Stock room never wires the existing deactivate/activate API calls (`ApiInventorySource.cs:66-69`).
- `IngredientDialog` supports an edit mode that locks name/unit and edits only the threshold (`IngredientDialog.xaml.cs:26-35`) — but it is dead code: the dialog is only constructed in create mode (`StockRoomPage.xaml.cs:568`), so ingredients have no edit action at all. Supplier edit is fully wired and works in both modes (`StockRoomPage.xaml.cs:746-783`).
- The Service board loads table data but never renders it (`OperationsWorkspace.cs:17,71`); `docs/service-board-ui.md` documents the missing seating overview.
- Mobile contract features unused by the app: order creation, confirm/preparing/ready/complete, discount requests, loyalty redeem, customer management, ingredient/receipt creation, threshold setting, logout-all (`contracts/mobile-p0/openapi.json`).
- Reports page has no export; loyalty ledger is display-only with no manual adjustment (`CustomerWorkspace.cs:69`).

</details>
