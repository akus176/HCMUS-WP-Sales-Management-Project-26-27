## Context

See `proposal.md` for motivation and `specs/staff-mobile-app/spec.md` for observable behavior. The repository currently contains a .NET modular monolith, a WinUI client, an ASP.NET Core API composition root, shared C# contracts, and PostgreSQL persistence. The current API is still being built toward the final architecture in which clients use `/api/v1`, JWT authentication, permission policies, and RFC 7807 errors.

The mobile client must share that final backend and business state with WinUI. It cannot reference C# contracts directly, cannot connect to PostgreSQL, and must preserve the existing order, delivery, inventory, payment, discount, and account rules. Android phones are the first deployment target, but the architecture must not prevent iOS or tablet support.

## Current scope decision (26 November 2026)

The owner selected Counter + Preparation. [The decision record](../../../docs/mobile-scope-206.md)
supersedes earlier six-role workspace assumptions and retired workflow rollout steps.
Counter includes draft sales, existing-customer lookup, discount requests, loyalty,
order/payment operations and eligible manager cancellation/assignment. Preparation
retains queue/detail transitions. More contains Account only. Windows approval,
inventory/receipts, courier and administration functions remain outside mobile scope.
The previous full-role evidence remains historical; #206 must record current selected
Android coverage and @longtoZ approval before closure.

## Goals / Non-Goals

**Goals:**

- Add a maintainable React Native client that fits the existing repository and API boundary.
- Keep authentication, authorization, validation, concurrency, auditing, and business transitions authoritative on the server.
- Expose Counter and Preparation from effective permissions without duplicating the complete desktop application.
- Provide prompt operational updates and notifications with safe behavior during network loss.
- Keep TypeScript API types synchronized with ASP.NET contracts through repeatable generation.
- Make the highest-risk workflows testable: authentication, Counter save/confirm/adjustment/payment retries, manager cancellation/assignment and Preparation transitions.

**Non-Goals:**

- Sharing UI code with WinUI or moving domain logic into JavaScript.
- Direct database access, offline write synchronization, or conflict merging.
- Replacing the full desktop POS, menu/recipe editor, reporting suite, AI assistant, or role-configuration interface.
- Employee scheduling, payroll, multi-branch support, customer self-service, third-party delivery integration, or background location tracking.
- Committing generated credentials, signing secrets, production push keys, or environment-specific API addresses.

## Decisions

### 1. Build the client with React Native, TypeScript, and Expo

Create `apps/staff-mobile` as an Expo application written in strict TypeScript. Use Expo Router for file-based navigation and development builds for secure storage, notifications, deep links, and other native integrations. Keep Android as the first tested platform while avoiding Android-only application abstractions.

This gives the team a productive cross-platform toolchain while retaining access to native capabilities. A bare React Native application was considered but rejected for the first release because it adds native project maintenance before the product requires custom native code. .NET MAUI was considered but rejected because React Native is the selected client technology.

### 2. Use the ASP.NET Core API as the only mobile business boundary

The mobile client calls the same versioned `/api/v1` endpoints used by the final WinUI client. It never references persistence models or database credentials. Domain transitions, permissions, idempotency, validation, and audit recording stay in the existing Domain, Application, and API layers.

Reuse existing authenticated catalog, customer lookup, order, payment, table-availability and preparation endpoints, operational events and device registration. Shared Windows endpoints remain published even when the corresponding mobile workspace is excluded.

A mobile-specific backend or direct database connection was rejected because either would duplicate rules and allow the Windows and mobile clients to disagree.

### 3. Generate the TypeScript client from the API's OpenAPI document

Generate request types, response types, and a thin HTTP client from the committed API contract or a deterministic development OpenAPI document. Pin the generator and expose one repeatable command that fails CI when regeneration produces an uncommitted diff. Wrap generated calls in feature-level query and mutation hooks; never hand-edit generated files.

Manually maintained TypeScript DTOs were rejected because the C# and TypeScript contracts would drift. Importing `CoffeeShop.Contracts` is impossible across the language boundary and would couple presentation concerns to server source code.

### 4. Separate server state, transient UI state, and forms

Use TanStack Query for API-backed state, caching, invalidation, retry policy, and refresh. Use Zustand only for small transient client state that does not belong in navigation or server caches, such as an active role filter. Use React Hook Form with Zod schemas for immediate input feedback while treating server validation as authoritative.

Do not create a general Redux-style duplicate of backend entities. Maintaining a second normalized business store was rejected because it increases stale-state and conflict risk.

### 5. Protect sessions with platform credential storage

Keep the short-lived access token in memory and the refresh token in Expo SecureStore. Serialize refresh attempts so simultaneous `401` responses cannot rotate the same refresh token more than once. On terminal refresh failure, account deactivation, logout, or token-reuse response, clear tokens, query caches, protected navigation history, and retained business data.

AsyncStorage and plain application configuration were rejected for tokens because neither provides an appropriate protected credential boundary.

### 6. Combine authenticated live events with push notifications

Use an authenticated SignalR hub for foreground updates. Events carry record identifiers, event type, and revision or update time rather than complete sensitive records; clients invalidate and refetch authorized API data. Group subscriptions are assigned server-side from the authenticated user's permissions and assignments.

Use Expo Notifications for background actionable alerts. Store device registrations server-side with user, platform, push token, last-seen time, and revocation state. Register or rotate the device token after authentication, unregister it on logout when possible, and prune invalid tokens after provider failures. Notification payloads contain an opaque record reference and route, not customer names, phone numbers, addresses, payment values, or access tokens.

Polling alone was rejected because it wastes mobile resources and delays urgent work. Sending full records through SignalR or push was rejected because it weakens permission re-evaluation and privacy.

### 7. Keep degraded operation read-only and session-scoped

TanStack Query may retain already loaded records in memory while the application remains open. When connectivity is unavailable, mutations are disabled and never placed in a background write queue. The UI displays offline or reconnecting state, preserves user-entered form data where safe, and requires an explicit retry after connection returns.

Persistent offline business caching and queued synchronization were rejected because they require encryption, conflict resolution, expiry rules, and domain-specific reconciliation outside the current system's scope. Counter delivery contact/address data and uncertain commands remain session-scoped; durable offline carts are excluded.

### 8. Organize navigation and features around permissions

The authenticated root derives accessible route groups from effective permission codes, not hard-coded role-name checks. Active feature folders own Counter and Preparation queries, mutations, validation and tests. Retired modules may remain as historical source but are unavailable through navigation and guarded routes. Deep-link guards resolve current permissions before fetching or rendering protected content.

The server remains authoritative; client route guards improve usability but are never treated as security enforcement.

### 9. Adapt the existing visual language for mobile operations

Implement shared mobile theme tokens matching the existing warm-paper palette, pencil foreground, red correction accent, blue informational accent, yellow highlights, and bundled Inter typography from the selected native redesign. Reduce rotation and decoration on dense operational lists, use stable irregular shapes rather than layout-time randomness, and keep functional icons conventional and labeled.

Use portrait-first layouts, responsive tablet composition, minimum 48-dp action targets, dynamic type, screen-reader labels, and contrast checks. Reproducing WinUI XAML components directly was rejected; the clients share design intent and tokens, not platform widgets.

### 10. Test contracts and critical workflows at multiple boundaries

Use unit and component tests for permissions, validation, error mapping, and screen behavior; mock API transport at the generated-client boundary. Use API integration tests for mobile-required queries, device registration, hub authorization, and permission filtering. Use mobile end-to-end tests for login, Counter save/confirm/customer/adjustment/payment, manager cancellation/assignment, Preparation progression, urgent Preparation links, offline protection and logout cleanup.

CI builds the Android application, validates TypeScript and lint rules, runs unit tests, checks generated-client drift, and runs backend tests. Device-dependent push delivery can use a provider fake in CI plus a documented real-device verification before release.

### 11. Deliver the application through evidence-gated phases

Use the scope decision rather than the former six-workspace target. P0 retains
sessions, permissions, Preparation, safe connectivity and shared-state behavior.
P1 covers Mobile 5.1–5.4 as mapped in #206. P2 retains release/accessibility/demo gates.
Historical reports remain tied to their actual revisions; full current acceptance
cannot be inferred from a checked old task or a green CI build.

## Risks / Trade-offs

- **[The mobile work depends on unfinished final API and identity endpoints]** → Implement against explicit OpenAPI contracts and provider fakes, then gate end-to-end completion on the relevant backend endpoints.
- **[Generated clients can produce noisy or unstable diffs]** → Pin the generator and configuration, normalize output, and regenerate only from a deterministic contract.
- **[Refresh-token rotation can race across requests]** → Use one refresh mutex and replay eligible failed requests only after successful renewal.
- **[SignalR events can be missed during suspension or reconnect]** → Treat events as invalidation hints, refetch authoritative lists after reconnect, and use push notifications for background urgency.
- **[Notifications can disclose customer or financial data]** → Use generic preview text and opaque identifiers, then reauthorize and fetch details after the user opens the app.
- **[Role names can imply unavailable mobile workspaces]** → Expose only Counter/Preparation actions justified by effective permissions; keep exclusions explicit in the function list.
- **[Online-only writes limit delivery use in weak coverage]** → Make connection state explicit and preserve safe form input, while postponing synchronization until the product has evidence that its complexity is necessary.
- **[Expo support may not cover a future native dependency]** → Use development builds and Expo prebuild; eject to maintained native projects only if a validated requirement cannot be met otherwise.

## Migration Plan

1. Scaffold `apps/staff-mobile` with pinned tooling, environment validation, theme tokens, navigation, and test harnesses.
2. Establish deterministic OpenAPI client generation and implement authenticated API transport with secure token rotation.
3. Add missing backend read endpoints, device registration, and the authenticated operational event hub behind existing permission policies.
4. Keep the Counter + Preparation layout, reconcile current scope and operation descriptions, and verify selected Counter/manager/device/notification flows under #206.
5. Add push registration, deep links, reconnect behavior, privacy cleanup, accessibility verification, and end-to-end tests.
6. Run Android internal testing against a staging-like API, validate multi-client synchronization with WinUI, and document physical-device network configuration.
7. Release behind an environment/configuration gate. Roll back by withdrawing the mobile build and disabling mobile device registrations/hub access; the shared API and WinUI workflows continue operating without mobile data migration.
