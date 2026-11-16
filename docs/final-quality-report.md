# Windows/shared final quality report (#204)

Verified on 16 November 2026 from an initially clean managed checkout of main
`5683fd14a56ef6d5a816288d769a894e7833970b`, with the focused #204 changes.
This report covers Windows/shared regression, deployment and quality tooling.
Final acceptance remains open for the runtime and reviewer gates below.

## Reproducible checks

The [machine-readable local record](evidence/final-quality-204/verification.json)
contains observed results; [API collection results](evidence/final-quality-204/api-collection.json)
retain all nine request/status/assertion outcomes. The
[negative run](evidence/final-quality-204/api-collection-negative.json) deliberately
uses a wrong manager password and must fail. It is preserved as a failure,
not counted as a passing API run.

| Check | Actual result | Reproduce / evidence |
| --- | --- | --- |
| Release solution build | Pass, zero warnings/errors with SDK 10.0.404 | `dotnet build CoffeeShop.slnx -c Release`; `global.json` prevents selecting SDK 11 |
| Formatting | Pass; workspace-load warning, no formatting violations | `dotnet format CoffeeShop.slnx --verify-no-changes --no-restore` |
| Domain | 413 passed, zero failed/skipped | `dotnet test tests/CoffeeShop.Domain.Tests -c Release` |
| Application | 422 passed, zero failed/skipped | `dotnet test tests/CoffeeShop.Application.Tests -c Release` |
| WinUI presentation/client | 151 passed, zero failed/skipped | `dotnet test tests/CoffeeShop.WinUI.Tests -c Release` |
| PostgreSQL infrastructure | 265 passed, zero failed/skipped | `dotnet test tests/CoffeeShop.Infrastructure.Tests -c Release`, disposable `COFFEESHOP_TEST_POSTGRES` |
| Complete API, including AI | 67 passed, zero failed/skipped | `dotnet test tests/CoffeeShop.Api.Tests -c Release`, same disposable server |
| Release dependency inspection | Pass, no PostgreSQL/EF/Npgsql or database-setting references | `./scripts/verify-winui-api-release.ps1 -SkipBuild` |
| Empty-volume Compose | Pass; PostgreSQL/API healthy and both health endpoints return `Healthy` | `npm ci --prefix tests/api`; `bash scripts/verify-compose.sh` |
| Migrations / schema | 11 migrations automatically applied; orders empty before seed | Same Compose runner; fresh unique project/volume and ephemeral loopback ports |
| Demo seed, twice | Both runs: 5 categories, 20 products, 40 sizes, 40 recipe rows, 9 ingredients, 3 suppliers, 2 receipts, 12 customers, 8 tables, 24 orders | Same runner checks exact counts and removes only its own volume |
| Role/account provisioning | Six roles; synthetic Manager and Cashier can sign in | Same runner, generated private password passed through environment only |
| Actual Postman collection | 9/9 requests, 9/9 assertions pass | Pinned Postman CLI; private temporary files removed; sanitized evidence linked above |
| Intentional wrong login | Correctly fails three dependent assertions; Manager 401, Cashier 200 | Negative collection evidence; categories remain exactly five |
| Collection runner guards | 5/5 tests pass | `npm test --prefix tests/api`; incomplete/skipped/stale evidence and secret export rejection |
| Secret scan | Reachable main history passes after three exact reviewed false-positive fingerprints | Gitleaks 8.24.3 with checksum verification; `bash scripts/verify-secrets.sh` in Linux CI |
| Secret negative fixture | Synthetic random credential rejected (exit 1) | Same scanner; all output redacted; no real credential used |
| NuGet audit | Zero vulnerable direct/transitive packages | `dotnet list CoffeeShop.slnx package --vulnerable --include-transitive --format json` |
| npm audits | Contract and API runner: zero findings; mobile: 28 moderate, 1 low, zero high/critical | Full audit reports retained in CI; owned follow-up [#216](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/216) |
| Dependency gate probes | Vulnerable NuGet, high/critical npm and incomplete/error reports rejected | `node scripts/check-dependency-audit.cjs --self-test` |

The isolated PostgreSQL tests create/drop their own databases. Missing database
configuration is not accepted as verification. Total: **1,318 tests passed**, zero
failed or skipped. CI now runs Domain/Application/
WinUI on Windows and the **complete** API suite with PostgreSQL, so API database
tests are no longer silently skipped in the Windows job. Both jobs retain TRX.
Existing mobile quality, Android build and generated-contract jobs remain enabled;
their PR-head results must be read from the linked CI run, not inferred from
backend test results.

## Regression and boundary matrix

| Behavior | Representative cases and assertions | Executable evidence |
| --- | --- | --- |
| Deactivated menu and history | Confirm two drinks, rename/reprice/deactivate the catalog item, reload historical lines; original Latte/Large/45,000 unit price and 90,000 total survive, new selection is rejected, no duplicate consumption | `ConfirmedHistory_RetainsNamesAndPricesAfterCatalogEditAndDeactivation` in [order confirmation integration tests](../tests/CoffeeShop.Infrastructure.Tests/OrderConfirmationIntegrationTests.cs) |
| Quantity validation | Zero/negative quantities rejected before an order line is accepted; invalid recipes/adjustments cannot mutate stock | Existing Domain/Application tests and [quantity tests](../tests/CoffeeShop.Domain.Tests/Common/MeasuredQuantityTests.cs) |
| Stock boundaries | Exactly 36 available for two 18-unit recipes reaches zero with one -36 movement; insufficient/multi-ingredient stock rolls back the entire order, every balance and movement | `Confirm_ExactStockBoundary_ConsumesToZeroWithoutOverdraft`, `Confirm_Shortage_RollsBackEverything`, `Confirm_MultiIngredientShortage_DeductsNothingFromAnyIngredient` |
| Payments | Partial balance survives reload and prevents completion; duplicate retries record once | [payment integration tests](../tests/CoffeeShop.Infrastructure.Tests/PaymentPersistenceIntegrationTests.cs), [payment domain tests](../tests/CoffeeShop.Domain.Tests/Sales/PaymentTests.cs) |
| Authorization | Anonymous 401, insufficient permission 403; allowed six-role paths; final administrator, refresh reuse, account revocation and ownership checks | [staff HTTP tests](../tests/CoffeeShop.Api.Tests/StaffIdentityHttpTests.cs), collection's 401/403 requests |
| AI schema and permission boundaries | Reversed periods/unsupported parameters rejected; missing report permissions return no metrics; non-manager denied | [tool gateway tests](../tests/CoffeeShop.Api.Tests/AssistantToolGatewayTests.cs) and new `Assistant204_EnforcesHttpPermissionsAndPersistsOnlyRedactedReadOnlyOutcomes` |
| AI grounding/refusal/audit | Nonempty response uses only returned revenue/count and selected period; empty reports explicit; mutation/unsupported questions invoke no tools; persisted audit uses authenticated actor, redacts synthetic credentials, creates no orders or stock movements | [assistant service tests](../tests/CoffeeShop.Api.Tests/ManagerAssistantServiceTests.cs) and new HTTP regression |
| Shared state and concurrency | Existing cancellation, confirmation, payment, table, loyalty and identity suites; separate paired native evidence retains its own prerequisites/review | [#202](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/202), [#203](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/203) |

## Fixes and quality policy

- SDK selection: the original unpinned command selected .NET 11 and failed with
  seven CsWinRT1032 errors. `global.json` selects installed stable .NET 10 feature
  bands; the Release build then passed without changing application code or
  disabling analyzers.
- README: direct mode must use **Debug** because Release deliberately excludes
  direct-database composition. Setup now distinguishes both modes, documents
  complete PostgreSQL-backed tests, role provisioning, CI and quality evidence.
- CI: checksum-verified history scanning, dependency auditing and their negative
  probes now run alongside build/format/test/migration checks. The existing #202
  collection runner is reused, rather than introducing a second runner. Its
  complete source is identical to the upstream implementation at `70839c1`.
- Dependency policy: reject every vulnerable NuGet package and high/critical npm
  findings, retain all lower npm advisories, and track remediation in owned #216.
  The current Expo/Jest glob/minimatch/deepmerge findings are **not fixed** or
  claimed clean. No forced downgrade or unverified transitive override is used.
- Scanner exceptions are exact historical fingerprints for two documented commit
  IDs and ordinary prose in the cancellation guide. They do not allowlist
  whole directories, token patterns or future secrets. The negative fixture
  originally used a predictable sequence excluded by the scanner's test-data
  rules; a generated random synthetic credential now proves actual rejection.
- Compose's provisioning commands emit an optional GSSAPI-library warning in the
  container. Password authentication, provisioning, readiness and all collection
  assertions still pass; the warning is retained as a nonfatal observation.

## Runtime evidence and remaining acceptance

The implementation of [#207](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/207)
is merged in [PR #212](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/212).
The issue is closed with runtime/reviewer checkboxes checked, but its linked PR
still explicitly records the external-provider WinUI demo as pending. No attached
external-provider WinUI screenshot/report or named-reviewer evidence was located
in that issue/PR or tracked documentation. Parent #26 task 12.6 also remains open.
These conflicting records do not establish fresh runtime verification.

The new API/assistant tests use the deterministic provider and are **not** the
required external-provider WinUI interaction. Follow the
[assistant demo guide](ai-manager-assistant.md) with a privately configured API
provider, capture the real question/period/answer/tool context and obtain the
required review. Do not commit keys or reuse unit tests as that evidence.

Starting a disposable native API/WinUI verification environment in this session
was rejected by automatic approval review with only `blocked by policy` returned.
No alternate launcher was used. A fresh native sign-in/assistant smoke test and
human contributor walkthrough are therefore unrun; the automated clean Docker
setup is verified separately. Existing [#200 native evidence](evidence/issue-200-winui-runtime/)
is historical and is not presented as this checkout's runtime result.

S4/S5 shared-state defects are owned by #202/#203 and their open PRs #214/#215;
their fixes/evidence are not silently included or approved by #204. Lower npm
advisories are owned in #216. The final recording, unlisted-video upload, Moodle
submission, production push delivery and external-provider credentials are outside
this regression PR. Keep #204 and parent final-acceptance checkboxes open until
the missing runtime evidence, exact-head CI and @akus176's approval are present.
