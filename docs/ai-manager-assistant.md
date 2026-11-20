# Manager reporting assistant

The assistant is on the **Reports** screen for signed-in managers and admins with `assistant.ask`. It can combine revenue, best sellers, order channels, and current low stock in one answer. The API checks the caller's `reports.*` permissions for each report. The provider plans which reports to use. The API calculates and formats the answer from report values, which stay on the server. Unsupported questions and requests to change business data call no report tool.

## Local setup and live demo

1. Apply the current EF Core migration to the same PostgreSQL database used by the API (`dotnet ef database update --project src/CoffeeShop.Infrastructure.Postgres --startup-project src/CoffeeShop.Infrastructure.Postgres`). Supply `ConnectionStrings__CoffeeShop` in the shell or ignored `.env`.
2. Configure a real OpenAI-compatible chat completions provider **outside Git**. Set `Assistant__Provider=openai`, `Assistant__Model` to the configured model, and `Assistant__ApiKey` to the key. `Assistant__Endpoint` is optional and defaults to `https://api.openai.com/v1/chat/completions`; any custom endpoint must be HTTPS. These settings belong in the API process environment, not the WinUI process. Do not put keys in screenshots, test fixtures, or CI logs.
3. Start the API and WinUI in API mode. Sign in as a manager whose account has `assistant.ask` and the relevant `reports.*` permissions. Open **Reports**, select a period, and ask “How did revenue and average order value change compared with the previous period, and which ingredients are low in stock?” Check that the answer compares the selected period with the preceding period of equal length and lists current stock separately. Ask “Cancel the last order” and confirm refusal with no tool call. Revoke `reports.bestsellers` from a test account and confirm it receives no seller data.
4. Capture a real WinUI screenshot of the question, grounded answer, period, and tool context for issue #207. Record the provider model and configuration keys used, without recording the key value. Reviewer `@longtoZ` must approve the evidence before closing that issue.

For deterministic local development, `Assistant__Provider=deterministic` selects reports by fixed keywords and requires no external provider. API tests use this mode. It is useful for UI smoke testing but does **not** satisfy the real-provider demo gate in issue #207.

The API writes one `reporting.ai_interaction_audits` row per authenticated assistant interaction with user ID, redacted question, selected tool and outcome, and duration. It does not persist the provider key or bearer token. Audit data should be accessible only through normal database administration; no audit read endpoint is exposed by the assistant.
