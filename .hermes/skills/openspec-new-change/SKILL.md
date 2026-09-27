---
name: openspec-new-change
description: Start a new OpenSpec change using the experimental artifact workflow. Use when the user wants to create a new feature, fix, or modification with a structured step-by-step approach.
allowed-tools: Bash(openspec:*), Bash(gh:*), Bash(git:*)
license: MIT
compatibility: Requires openspec CLI.
metadata:
  author: openspec
  version: "1.0"
  generatedBy: "1.10.0"
---


## Project override: GitHub issue ticket tracking

This repository does **not** use `tasks.md`; it uses a GitHub issue ticket instead. This section takes precedence over every later reference to a tasks artifact, tasks file, `contextFiles.tasks`, or task checkboxes.

- Track implementation work in exactly one GitHub issue per OpenSpec change. The issue title is `OpenSpec: <change-name>` and its body contains the exact marker `<!-- openspec-change: <change-name> -->`.
- Resolve the repository from `git remote get-url origin`. Use `gh issue list --state all --search "OpenSpec: <change-name> in:title" --json number,title,body,state,url` and accept only an issue whose body contains the exact marker. Reuse and update it; never create a duplicate.
- When the workflow reaches the schema's `tasks` artifact, run its OpenSpec instructions for planning guidance, but create or update the issue instead of writing the returned `resolvedOutputPath`. The issue body must contain `## Summary`, `## OpenSpec Artifacts`, `## Implementation Checklist`, and `## Acceptance Criteria`. Put implementation work under `## Implementation Checklist` as `- [ ]` items.
- Treat that issue as satisfying the project's implementation-plan requirement. The stock schema may continue to report the `tasks` artifact as missing or blocked because no local file exists; do not create a placeholder file and do not let that expected state block propose, continue, fast-forward, apply, verify, or archive. Other missing artifacts still block normally.
- For apply and verify, read proposal/spec/design from `artifactPaths` or `contextFiles`, then read the issue with `gh issue view <number> --json number,title,body,state,url`. The issue checklist is the canonical progress source.
- After completing an item, edit only that item's checkbox in the issue body (`- [ ]` to `- [x]`) and read the issue back before claiming progress. Never mark partial or deferred work complete.
- Every commit for the change must use `<type>(<scope>): <imperative summary> (#<issue-number>)`, using the linked issue's real number. Keep commits atomic and use an imperative summary. Examples:
  - `feat(catalog): add menu item lifecycle rules (#12)`
  - `test(domain): cover money validation (#2)`
  - `infra(postgres): add initial migration (#8)`
  - `docs(workflow): align ownership with five-member team (#11)`
- After committing, verify the subject with `git log -1 --format=%s`; do not accept a missing, placeholder, or mismatched issue number.
- After a successful archive, close the issue as completed if every checklist item is checked; otherwise leave it open and report the incomplete items.
- After every issue create, edit, or close, read the exact issue back and verify the marker, body/state, and intended change before reporting success.
- If `gh` is unavailable, authentication fails, no GitHub remote exists, or multiple marked tickets are found, stop and report the blocker. Never fall back to `tasks.md`.

Start a new change using the experimental artifact-driven approach.

**Store selection:** If the user names a store (a store is a standalone OpenSpec repo registered on this machine) or the work lives in one, run `openspec store list --json` to discover registered store ids, then pass `--store <id>` on the commands that read or write specs and changes (`new change`, `status`, `instructions`, `list`, `show`, `validate`, `archive`, `doctor`, `context`, `schemas`, `view`). Once selected, treat `--store <id>` as sticky for the rest of the workflow. Every unscoped example of those commands below is shorthand: before running it, append the flag. For example, run `openspec status --change "<name>" --json --store "<id>"`, not the unscoped form shown below. Other commands do not take the flag. Hints printed by commands already carry the flag; keep it on follow-ups. Without a store, commands act on the nearest local `openspec/` root.

**Input**: The user's request should include a change name (kebab-case) OR a description of what they want to build.

**Steps**

1. **If no clear input provided, ask what they want to build**

   Ask the user (open-ended, no preset options):
   > "What change do you want to work on? Describe what you want to build or fix."

   From their description, derive a kebab-case name (e.g., "add user authentication" → `add-user-auth`).

   **IMPORTANT**: Do NOT proceed without understanding what the user wants to build.

2. **Determine the workflow schema**

   Use the default schema (omit `--schema`) unless the user explicitly requests a different workflow.

   **Use a different schema only if the user mentions:**
   - A specific schema name → use `--schema <name>`
   - "show workflows" or "what workflows" → run `openspec schemas --json` and let them choose

   **Otherwise**: Omit `--schema` to use the default.

3. **Create the change directory**
   ```bash
   openspec new change "<name>"
   ```
   Add `--schema <name>` only if the user requested a specific workflow.
   This creates a scaffolded change in the planning home resolved by the CLI.

4. **Show the artifact status**
   ```bash
   openspec status --change "<name>" --json
   ```
   Use the returned `planningHome`, `changeRoot`, `artifactPaths`, and `nextSteps` instead of assuming repo-local paths.

5. **Get instructions for the first artifact**
   The first artifact depends on the schema (e.g., `proposal` for spec-driven).
   Check the status output to find the first artifact with status "ready".
   ```bash
   openspec instructions <first-artifact-id> --change "<name>"
   ```
   This outputs the template and context for creating the first artifact.

6. **STOP and wait for user direction**

**Output**

After completing the steps, summarize:
- Change name and location
- Schema/workflow being used and its artifact sequence
- Current status (0/N artifacts complete)
- The template for the first artifact
- Prompt: "Ready to create the first artifact? Just describe what this change is about and I'll draft it, or ask me to continue."

**Guardrails**
- Do NOT create any artifacts yet - just show the instructions
- Do NOT advance beyond showing the first artifact template
- If the name is invalid (not kebab-case), ask for a valid name
- If a change with that name already exists, suggest continuing that change instead
- Pass --schema if using a non-default workflow
