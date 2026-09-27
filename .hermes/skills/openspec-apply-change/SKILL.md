---
name: openspec-apply-change
description: Implement an OpenSpec change from its GitHub issue checklist. Use when the user wants to start or continue implementation.
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

Implement checklist items from the GitHub issue linked to an OpenSpec change.

**Store selection:** If the user names a store (a store is a standalone OpenSpec repo registered on this machine) or the work lives in one, run `openspec store list --json` to discover registered store ids, then pass `--store <id>` on the commands that read or write specs and changes (`new change`, `status`, `instructions`, `list`, `show`, `validate`, `archive`, `doctor`, `context`, `schemas`, `view`). Once selected, treat `--store <id>` as sticky for the rest of the workflow. Every unscoped example of those commands below is shorthand: before running it, append the flag. For example, run `openspec status --change "<name>" --json --store "<id>"`, not the unscoped form shown below. Other commands do not take the flag. Hints printed by commands already carry the flag; keep it on follow-ups. Without a store, commands act on the nearest local `openspec/` root.

**Input**: Optionally specify a change name (e.g., `/openspec-apply-change add-auth`). If omitted, check if it can be inferred from conversation context. If vague or ambiguous you MUST prompt for available changes.

**Steps**

1. **Select the change**

   If a name is provided, use it. Otherwise:
   - Infer from conversation context if the user mentioned a change
   - Auto-select if only one active change exists
   - If ambiguous, run `openspec list --json` to get available changes and ask the user to select one

   Always announce: "Using change: <name>" and how to override (e.g., `/openspec-apply-change <other>`).

2. **Check status to understand the schema**
   ```bash
   openspec status --change "<name>" --json
   ```
   Parse the JSON to understand:
   - `schemaName`: The workflow being used (e.g., "spec-driven")
   - `planningHome`, `changeRoot`, and `actionContext`: planning scope and edit constraints
   - Whether the schema exposes a `tasks` artifact; in this project it is represented by the marked GitHub issue instead of a file

3. **Get apply instructions**

   ```bash
   openspec instructions apply --change "<name>" --json
   ```

   This returns:
   - `contextFiles`: artifact ID -> array of concrete file paths (varies by schema - could be proposal/specs/design/tasks or spec/tests/implementation/docs)
   - Progress (total, complete, remaining)
   - Task list with status
   - Dynamic instruction based on current state
   - Optional `context`: current required project instruction input from the selected root
   - Optional `operationGuidance`: current advisory guidance for apply

   **Handle states:**
   - If `state: "blocked"` (missing artifacts): show message, suggest using `/openspec-continue-change` (if it is not installed, run `openspec status --change "<name>" --json` to see the next artifact and `openspec instructions <artifact-id> --change "<name>" --json` for how to create it)
   - If `state: "all_done"`: congratulate, suggest archive
   - Otherwise: proceed to implementation

   Treat `context` as a required prompt-level input. Read and consider it, and
   apply relevant project facts, conventions, and constraints while implementing.
   Treat `operationGuidance` as optional additive advice. Read and consider every
   entry, and follow entries that are applicable and compatible with the built-in
   workflow.

   Keep both fields separate from CLI-returned state, missing artifacts, tasks,
   progress, `contextFiles`, and the built-in `instruction`. They are not
   evidence of task completion, do not replace the built-in instruction, and do
   not permit bypassing a blocked state. If context conflicts with the built-in
   instruction, an explicit user choice, or a CLI-controlled value, report the
   conflict and preserve the controlling value. If guidance is inapplicable or
   conflicts with those controlling inputs, do not follow it and explain why.
   These are prompt-level behavior contracts, not enforceable checks.

4. **Read context files**

   Read every file path listed under `contextFiles` from the apply instructions output.
   The files depend on the schema being used:
   - **spec-driven**: proposal, specs, and design from `contextFiles`; load implementation work from the marked GitHub issue
   - Other schemas: follow the contextFiles from CLI output

   Do not copy `context` or `operationGuidance` verbatim into implementation
   files or planning artifacts unless the user separately asks for that content.

5. **Show current progress**

   Display:
   - Schema being used
   - Progress: "N/M tasks complete"
   - Remaining tasks overview
   - Dynamic instruction from CLI

6. **Implement tasks (loop until done or blocked)**

   For each pending task:
   - Show which task is being worked on
   - Make the code changes required
   - Keep changes minimal and focused
   - Mark the matching checklist item complete in the GitHub issue body, then read the issue back to verify the update
   - Continue to next task

   **Pause if:**
   - Task is unclear → ask for clarification
   - Implementation reveals a design issue → suggest updating artifacts
   - A task needs work beyond what the spec and tasks describe, or you are tempted to drop, narrow, defer, or accept exceptions to specified behavior to make it fit → surface the added scope and ask; do not absorb it silently
   - Error or blocker encountered → report and wait for guidance
   - User interrupts

7. **On completion or pause, show status**

   Display:
   - Tasks completed this session
   - Overall progress: "N/M tasks complete"
   - If all done: suggest archive
   - If paused: explain why and wait for guidance

**Output During Implementation**

```
## Implementing: <change-name> (schema: <schema-name>)

Working on task 3/7: <task description>
[...implementation happening...]
✓ Task complete

Working on task 4/7: <task description>
[...implementation happening...]
✓ Task complete
```

**Output On Completion**

```
## Implementation Complete

**Change:** <change-name>
**Schema:** <schema-name>
**Progress:** 7/7 tasks complete ✓

### Completed This Session
- [x] Task 1
- [x] Task 2
...

All tasks complete! You can archive this change with `/openspec-archive-change`.
```

**Output On Pause (Issue Encountered)**

```
## Implementation Paused

**Change:** <change-name>
**Schema:** <schema-name>
**Progress:** 4/7 tasks complete

### Issue Encountered
<description of the issue>

**Options:**
1. <option 1>
2. <option 2>
3. Other approach

What would you like to do?
```

**Guardrails**
- Keep going through tasks until done or blocked
- Always read context files before starting (from the apply instructions output)
- If task is ambiguous, pause and ask before implementing
- If implementation reveals issues, pause and suggest artifact updates
- Keep code changes minimal and scoped to each task
- Update the issue checkbox immediately after completing each checklist item and verify it by reading the issue back
- Pause on errors, blockers, or unclear requirements - don't guess
- When a task needs work beyond what the spec describes, surface the added scope and pause - never silently narrow, defer, or simplify away specified behavior
- Only mark a task `- [x]` when its specified behavior is fully implemented, not when it is partially done or deferred
- Use `contextFiles` for local artifacts and the marked GitHub issue for the implementation checklist
- Do not use context or operation guidance as proof that a task is complete
- Apply relevant project context; report conflicts with controlling workflow inputs
- Consider every guidance entry; explain any inapplicable or conflicting advice
- Do not copy runtime context or operation guidance into implementation files or planning artifacts
- Preserve CLI-controlled blocked/ready/all-done behavior and completion criteria

**Fluid Workflow Integration**

This skill supports the "actions on a change" model:

- **Can be invoked anytime**: Before all artifacts are done (if tasks exist), after partial implementation, interleaved with other actions
- **Allows artifact updates**: If implementation reveals design issues, suggest updating artifacts - not phase-locked, work fluidly
