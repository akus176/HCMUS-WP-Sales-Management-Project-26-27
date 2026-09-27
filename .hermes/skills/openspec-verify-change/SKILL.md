---
name: openspec-verify-change
description: Verify implementation matches change artifacts. Use when the user wants to validate that implementation is complete, correct, and coherent before archiving.
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

Verify that an implementation matches the change artifacts and the linked GitHub issue checklist.

**Store selection:** If the user names a store (a store is a standalone OpenSpec repo registered on this machine) or the work lives in one, run `openspec store list --json` to discover registered store ids, then pass `--store <id>` on the commands that read or write specs and changes (`new change`, `status`, `instructions`, `list`, `show`, `validate`, `archive`, `doctor`, `context`, `schemas`, `view`). Once selected, treat `--store <id>` as sticky for the rest of the workflow. Every unscoped example of those commands below is shorthand: before running it, append the flag. For example, run `openspec status --change "<name>" --json --store "<id>"`, not the unscoped form shown below. Other commands do not take the flag. Hints printed by commands already carry the flag; keep it on follow-ups. Without a store, commands act on the nearest local `openspec/` root.

**Input**: Optionally specify a change name. If omitted, check if it can be inferred from conversation context. If vague or ambiguous you MUST prompt for available changes.

**Steps**

1. **Select the change**

   If a name is provided, use it. Otherwise:
   - Infer from conversation context if the user mentioned a change
   - Auto-select if only one active change exists
   - If ambiguous, run `openspec list --json` to get available changes and ask the user to select one

   When prompting, show changes that have a marked GitHub issue ticket.
   Include the schema used for each change if available.
   Mark changes with unchecked issue items as "(In Progress)".

   Always announce: "Using change: <name>" and how to override (e.g., `/openspec-verify-change <other>`).

2. **Check status to understand the schema**
   ```bash
   openspec status --change "<name>" --json
   ```
   Parse the JSON to understand:
   - `schemaName`: The workflow being used (e.g., "spec-driven")
   - `planningHome`, `changeRoot`, `artifactPaths`, and `actionContext`: path and scope context
   - Which artifacts exist for this change

3. **Get planning context and load artifacts**

   ```bash
   openspec instructions apply --change "<name>" --json
   ```

   This returns the change directory and `contextFiles` (artifact ID -> array of concrete file paths). Read all available artifacts from `contextFiles`.

4. **Initialize verification report structure**

   Create a report structure with three dimensions:
   - **Completeness**: Track tasks and spec coverage
   - **Correctness**: Track requirement implementation and scenario coverage
   - **Coherence**: Track design adherence and pattern consistency

   Each dimension can have CRITICAL, WARNING, or SUGGESTION issues.

5. **Verify Completeness**

   **Task Completion**:
   - Read the marked GitHub issue body
   - Parse checklist items: `- [ ]` (incomplete) vs `- [x]` (complete)
   - Count complete vs total tasks
   - If incomplete tasks exist:
     - Add CRITICAL issue for each incomplete task
     - Recommendation: "Complete task: <description>" or "Mark as done if already implemented"

   **Spec Coverage**:
   - If delta specs exist in `contextFiles.specs`:
     - Extract all requirements (marked with "### Requirement:")
     - For each requirement:
       - Search codebase for keywords related to the requirement
       - Assess if implementation likely exists
     - If requirements appear unimplemented:
       - Add CRITICAL issue: "Requirement not found: <requirement name>"
       - Recommendation: "Implement requirement X: <description>"

6. **Verify Correctness**

   **Requirement Implementation Mapping**:
   - For each requirement from delta specs:
     - Search codebase for implementation evidence
     - If found, note file paths and line ranges
     - Assess if implementation matches requirement intent
     - If divergence detected:
       - Add WARNING: "Implementation may diverge from spec: <details>"
       - Recommendation: "Review <file>:<lines> against requirement X"

   **Scenario Coverage**:
   - For each scenario in delta specs (marked with "#### Scenario:"):
     - Check if conditions are handled in code
     - Check if tests exist covering the scenario
     - If scenario appears uncovered:
       - Add WARNING: "Scenario not covered: <scenario name>"
       - Recommendation: "Add test or implementation for scenario: <description>"

7. **Verify Coherence**

   **Design Adherence**:
   - If `contextFiles.design` exists:
     - Extract key decisions (look for sections like "Decision:", "Approach:", "Architecture:")
     - Verify implementation follows those decisions
     - If contradiction detected:
       - Add WARNING: "Design decision not followed: <decision>"
       - Recommendation: "Update implementation or revise design.md to match reality"
   - If no design.md: Skip design adherence check, note "No design.md to verify against"

   **Code Pattern Consistency**:
   - Review new code for consistency with project patterns
   - Check file naming, directory structure, coding style
   - If significant deviations found:
     - Add SUGGESTION: "Code pattern deviation: <details>"
     - Recommendation: "Consider following project pattern: <example>"

8. **Generate Verification Report**

   **Summary Scorecard**:
   ```markdown
   ## Verification Report: <change-name>

   ### Summary
   | Dimension    | Status           |
   |--------------|------------------|
   | Completeness | X/Y tasks, N reqs|
   | Correctness  | M/N reqs covered |
   | Coherence    | Followed/Issues  |
   ```

   **Issues by Priority**:

   1. **CRITICAL** (Must fix before archive):
      - Incomplete tasks
      - Missing requirement implementations
      - Each with specific, actionable recommendation

   2. **WARNING** (Should fix):
      - Spec/design divergences
      - Missing scenario coverage
      - Each with specific recommendation

   3. **SUGGESTION** (Nice to fix):
      - Pattern inconsistencies
      - Minor improvements
      - Each with specific recommendation

   **Final Assessment**:
   - If CRITICAL issues: "X critical issue(s) found. Fix before archiving."
   - If only warnings: "No critical issues. Y warning(s) to consider. Ready for archive (with noted improvements)."
   - If all clear: "All checks passed. Ready for archive."

**Verification Heuristics**

- **Completeness**: Focus on objective checklist items (checkboxes, requirements list)
- **Correctness**: Use keyword search, file path analysis, reasonable inference - don't require perfect certainty
- **Coherence**: Look for glaring inconsistencies, don't nitpick style
- **False Positives**: When uncertain, prefer SUGGESTION over WARNING, WARNING over CRITICAL
- **Actionability**: Every issue must have a specific recommendation with file/line references where applicable

**Graceful Degradation**

- If only the GitHub issue ticket exists: verify checklist completion only, skip spec/design checks
- If the issue ticket and specs exist: verify completeness and correctness, skip design
- If full artifacts: verify all three dimensions
- Always note which checks were skipped and why

**Output Format**

Use clear markdown with:
- Table for summary scorecard
- Grouped lists for issues (CRITICAL/WARNING/SUGGESTION)
- Code references in format: `file.ts:123`
- Specific, actionable recommendations
- No vague suggestions like "consider reviewing"
