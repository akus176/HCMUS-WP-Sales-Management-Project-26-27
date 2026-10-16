## Why

Coffee-shop staff need a portable way to act on orders, approvals, deliveries, and inventory while moving around the shop or completing deliveries. A shared mobile client will extend the existing management system without duplicating its business rules or creating a separate source of operational data.

## What Changes

- Add an Android-first React Native staff application, built with TypeScript and Expo, that remains compatible with iOS and responsive tablet layouts.
- Serve admin, manager, cashier, barista, inventory staff, and delivery staff through permission-aware navigation and role-specific workflows.
- Use the same ASP.NET Core API, authentication, authorization, business rules, and PostgreSQL data as the final WinUI client.
- Provide mobile workflows for the barista preparation queue, manager approvals, order and table monitoring, delivery fulfillment, inventory operations, and limited account/security administration.
- Add actionable notifications and near-real-time updates for operational events, with safe manual refresh behavior when live updates are unavailable.
- Keep the client online-first: cached operational details may remain readable, but state-changing actions require a confirmed server response.
- Generate the TypeScript API client from the backend OpenAPI document so mobile contracts remain aligned with the server.

## Capabilities

### New Capabilities

- `staff-mobile-app`: Defines role-aware mobile access, supported staff workflows, live updates and notifications, connectivity behavior, security, and accessibility for the shared coffee-shop backend.

### Modified Capabilities

None. The mobile client consumes the authentication, sales, inventory, and administration behavior already planned for the shared backend without changing those domain requirements.

## Impact

- Adds a new `apps/staff-mobile` React Native application and its test, configuration, and generated API-client assets.
- Adds mobile-specific dependencies for Expo routing, secure storage, notifications, server-state management, forms, validation, and real-time communication.
- Requires the ASP.NET Core API to expose the existing staff workflows through stable OpenAPI contracts and to provide authenticated real-time events and notification registration endpoints.
- Requires mobile deep links, device-token lifecycle handling, protected local token storage, and privacy controls for customer delivery information.
- Does not add direct PostgreSQL access, offline writes, a second backend, multi-branch support, employee scheduling, payroll, customer ordering, or a mobile replacement for the complete desktop administration and reporting experience.
