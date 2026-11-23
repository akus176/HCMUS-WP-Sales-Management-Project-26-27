# Staff Mobile App — P0 Demonstration Script

Record one continuous, Unlisted YouTube video against a current API and seeded PostgreSQL database.
Do not show passwords, PATs, access tokens, refresh tokens, or local connection strings.

## Before recording

1. Record the mobile build commit, API commit, seed version, device or emulator, and test-run link.
2. Confirm the API is reachable from the Android device or emulator.
3. Prepare one staff account for each demonstrated P0 role.
4. Prepare deterministic records for a confirmed order, a pending discount, a low-stock ingredient, and an assigned delivery. Use WinUI or the shared API fixtures to draft and confirm the order and submit the discount request before recording. [Mobile sales operation map](../evidence/issue-232-mobile-sales/README.md)
5. Confirm the function list marks only demonstrated and verified functions as Implemented.

## Continuous P0 script

1. Open the repository and identify the staff-mobile OpenSpec and function list.
2. Launch the mobile application and sign in as a barista.
3. Show permission-aware navigation and the preparation queue.
4. Start a confirmed ticket, then mark it ready.
5. In the Windows client or a second authorized client, show the same ticket state after refresh or live update.
6. Sign in as a manager and approve or reject a pending discount.
7. Show the server-confirmed decision and total in the mobile approval result, then show the same affected order in WinUI.
8. Sign in as inventory staff, open low-stock inventory, and submit one valid adjustment with a reason.
9. Show the resulting balance and movement history.
10. Sign in as delivery staff, open only the assigned delivery, launch navigation, mark it out for delivery, and then mark it delivered or failed.
11. If a failure is demonstrated, show that a reason is required.
12. Disable connectivity, show the visible offline state, and show that a state-changing action is disabled or rejected without a local success claim.
13. Sign out and show that protected navigation no longer exposes the previous data.
14. End with the current CI/test evidence and the final function-list status.

## Evidence checklist

| Proof | Required evidence |
| --- | --- |
| Shared backend | A P0 update becomes visible to an authorized Windows or mobile client. |
| Permission safety | Each role sees only its permitted modules and records. |
| Server authority | Invalid, stale, or offline changes do not claim local success. |
| Privacy | No sensitive data appears in notification previews or recording setup. |
| Traceability | Each shown workflow has a function-list row, test evidence, and video timestamp. |

## After recording

1. Set the video to Unlisted.
2. Add exact timestamps to `mobile-function-list.md`.
3. Add the video URL to the final submission form or LMS record required by the course.
4. Verify the published video does not show a secret or planned-only function.
