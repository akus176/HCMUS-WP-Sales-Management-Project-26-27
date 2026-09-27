---
name: openspec-continue-change
description: Continue working on an OpenSpec change by creating the next artifact. Use when the user wants to progress their change, create the next artifact, or continue their workflow.
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
- Resolve `OWNER_REPO` from `git remote get-url origin` (strip the GitHub host prefix and trailing `.git`). Pass `--repo "$OWNER_REPO"` to every `gh issue` command, including create, list, view, edit, comment, and close. Find candidates with `gh issue list --repo "$OWNER_REPO" --state all --search "OpenSpec: <change-name> in:title" --json number,title,body,state,url`, then accept exactly one issue only when both its title equals `OpenSpec: <change-name>` and its body contains the exact marker. Reuse and update it; never create a duplicate.
- When the workflow reaches the schema's `tasks` artifact, run its OpenSpec instructions for planning guidance, but create or update the issue instead of writing the returned `resolvedOutputPath`. The issue body must contain `## Summary`, `## OpenSpec Artifacts`, `## Implementation Checklist`, and `## Acceptance Criteria`. Put implementation work under `## Implementation Checklist` as `- [ ]` items.
- Treat that issue as satisfying the project's implementation-plan requirement. The stock schema may continue to report the `tasks` artifact as missing or blocked because no local file exists; do not create a placeholder file and do not let that expected state block propose, continue, fast-forward, apply, verify, or archive. Other missing artifacts still block normally.
- For apply and verify, read proposal/spec/design from `artifactPaths` or `contextFiles`, then read the issue with `gh issue view <number> --repo "$OWNER_REPO" --json number,title,body,state,url`. The issue checklist is the canonical progress source.
- After completing an item, edit only that item's checkbox in the issue body (`- [ ]` to `- [x]`) and read the issue back before claiming progress. Never mark partial or deferred work complete.
- Every commit for the change must use `<type>(<scope>): <imperative summary> (#<issue-number>)`, using the linked issue's real number. Keep commits atomic and use an imperative summary. Examples:
  - `feat(catalog): add menu item lifecycle rules (#12)`
  - `test(domain): cover money validation (#2)`
  - `infra(postgres): add initial migration (#8)`
  - `docs(workflow): align ownership with five-member team (#11)`
- After committing, verify the subject with `git log -1 --format=%s`; do not accept a missing, placeholder, or mismatched issue number.
- After a successful archive, close the issue as completed if every checklist item is checked; otherwise leave it open and report the incomplete items.
- After every issue create, edit, or close, read the exact issue back and verify the marker, body/state, and intended change before reporting success.
- Legacy migration: if no marked issue exists but `artifactPaths.tasks.existingOutputPaths` identifies an existing `tasks.md`, read it once, create the canonical marked issue with the same checkbox text and checked state, read the issue back, and then treat the issue as authoritative. Do not update or recreate the legacy file after migration.
- If `gh` is unavailable, authentication fails, no GitHub remote exists, or multiple marked tickets are found, stop and report the blocker. Never fall back to `tasks.md`.

Continue working on a change by creating the next artifact.

**Store selection:** If the user names a store (a store is a standalone OpenSpec repo registered on this machine) or the work lives in one, run `openspec store list --json` to discover registered store ids, then pass `--store <id>` on the commands that read or write specs and changes (`new change`, `status`, `instructions`, `list`, `show`, `validate`, `archive`, `doctor`, `context`, `schemas`, `view`). Once selected, treat `--store <id>` as sticky for the rest of the workflow. Every unscoped example of those commands below is shorthand: before running it, append the flag. For example, run `openspec status --change "<name>" --json --store "<id>"`, not the unscoped form shown below. Other commands do not take the flag. Hints printed by commands already carry the flag; keep it on follow-ups. Without a store, commands act on the nearest local `openspec/` root.

**Input**: Optionally specify a change name. If omitted, check if it can be inferred from conversation context. If vague or ambiguous you MUST prompt for available changes.

**Steps**

1. **Select the change**

   If a name is provided, use it. Otherwise:
   - Infer from conversation context if the user mentioned a change
   - Auto-select if only one active change exists
   - If ambiguous, run `openspec list --json` to get available changes sorted by most recently modified, and ask the user to select one

   When prompting, present the top 3-4 most recently modified changes as options, showing:
   - Change name
   - Schema (from `schema` field if present, otherwise "spec-driven")
   - Status from the issue checklist (e.g., "0/5 items", "complete", "no issue")
   - How recently it was modified (from `lastModified` field)

   Mark the most recently modified change as "(Recommended)" since it's likely what the user wants to continue.

   Always announce: "Using change: <name>" and how to override (e.g., `/openspec-continue-change <other>`).

2. **Check current status**
   ```bash
   openspec status --change "<name>" --json
   ```
   Parse the JSON to understand current state. The response includes:
   - `schemaName`: The workflow schema being used (e.g., "spec-driven")
   - `artifacts`: Array of artifacts with their status ("done", "skipped", "ready", "blocked")
   - `isPlanningComplete`: Boolean indicating if all planning artifacts are complete. Older CLI versions expose the same value as `isComplete`.
   - `planningHome`, `changeRoot`, `artifactPaths`, and `actionContext`: path and scope context. Use these instead of assuming repo-local paths.

3. **Act based on status**:

   ---

   First compute effective planning completion: it is true when the CLI reports completion, **or** every non-`tasks` artifact is `done`/`skipped` and a unique valid marked issue satisfies `tasks`.

   **If effective planning completion is true**:
   - Congratulate the user
   - Show final status including the schema used
   - Suggest: "Planning is complete! You can now implement this change. Once implementation and any tracked work are complete, archive it."
   - STOP

   ---

   **If artifacts are ready to create** (status shows artifacts with `status: "ready"`):
   - Pick the FIRST artifact with `status: "ready"` from the status output
   - Get its instructions:
     ```bash
     openspec instructions <artifact-id> --change "<name>" --json
     ```
   - Parse the JSON. The key fields are:
     - `context`: Project background (constraints for you - do NOT include in output)
     - `rules`: Artifact-specific rules (constraints for you - do NOT include in output)
     - `template`: The structure to use for your output file
     - `instruction`: Schema-specific guidance
     - `resolvedOutputPath`: Resolved path or pattern to write the artifact
     - `dependencies`: Completed artifacts to read for context (entries with `skipped: true` have no files - do not look for them)
     - `skipped`/`warning`: present when the change declares skip_specs and this artifact must NOT be created - pick another artifact
   - **Create the artifact**:
     - Read any completed dependency files for context - always re-read them from disk, even if you saw them earlier in the conversation (the user may have edited them)
     - If `<artifact-id>` is `tasks`, create or update the canonical marked issue, read it back, and treat `tasks` as externally satisfied. Do not write `resolvedOutputPath`.
     - Else if the `instruction` field delegates creation to a specific skill or command, invoke it to produce the artifact instead of writing the file yourself, then verify the artifact file exists at `resolvedOutputPath`
     - Otherwise use `template` as the structure, apply `context` and `rules`, and write to `resolvedOutputPath` (choosing a concrete path when it is a glob)
   - Show what was created and what's now unlocked
   - STOP after creating ONE artifact

   ---

   **If no artifacts are ready (all blocked)**:
   - If every non-`tasks` artifact is `done`/`skipped` and the marked issue is valid, report effective planning completion and stop.
   - Otherwise show the unresolved non-`tasks` blockers and suggest checking the schema or artifacts.

4. **After creating an artifact, show progress**
   ```bash
   openspec status --change "<name>"
   ```

**Output**

After each invocation, show:
- Which artifact was created
- Schema workflow being used
- Current progress (N/M complete)
- What artifacts are now unlocked
- Prompt: "Want to continue? Just ask me to continue or tell me what to do next."

**Artifact Creation Guidelines**

The artifact types and their purpose depend on the schema. Follow the `instruction` field for local artifacts. When the selected artifact is `tasks`, use its guidance to create or update the GitHub issue ticket under the project override instead of writing a file.

If the `instruction` field directs you to use a specific skill or command to create the artifact, invoke it instead of writing the artifact directly.

**Guardrails**
- Create ONE artifact per invocation
- Always read dependency artifacts before creating a new one - re-read from disk, not from conversation memory (files may have changed since you last saw them)
- Never skip artifacts or create out of order
- If context is unclear, ask the user before creating
- Verify local artifact files after writing; for `tasks`, verify the marked issue by reading it back
- Use the schema's artifact sequence, don't assume specific artifact names
- **IMPORTANT**: `context` and `rules` are constraints for YOU, not content for the file
  - Do NOT copy `<context>`, `<rules>`, `<project_context>` blocks into the artifact
  - These guide what you write, but should never appear in the output
