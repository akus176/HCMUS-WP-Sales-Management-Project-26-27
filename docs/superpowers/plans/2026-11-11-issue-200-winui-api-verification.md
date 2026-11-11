# Issue 200 WinUI API Verification Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add reproducible automated coverage for the WinUI inventory and delivery API flows in issue #200 and record exactly which runtime evidence remains to be captured on Windows.

**Architecture:** Exercise the existing authenticated `ApiInventorySource` and `ApiOperationsSource` through an in-memory HTTP handler, asserting API routes, payloads, authorization, and authoritative reload data. Add an issue-specific evidence record that separates passing automated checks from pending native Windows UI/network checks.

**Tech Stack:** .NET 10, xUnit, `HttpClient`, existing shared API contracts.

**Spec:** `openspec/changes/build-coffee-shop-management-system/tasks.md` tasks 11.3, 11.4, and 11.7; GitHub issue #200.

## Tasks

- [x] Run the WinUI test suite to establish the current baseline (134 passed before changes).
- [x] Add tests for inventory receipt posting, adjustments, low-stock-relevant balances, and movement history through authenticated HTTP.
- [x] Add tests for delivery board reads, assignment, eligible transitions, authorization failures, and state conflicts through authenticated HTTP.
- [x] Run relevant tests and targeted formatting; record only observed results and explicitly list the native Windows build/runtime checks that cannot run here.
- [x] Open draft PR #208 referencing #200; defer the reviewer request until required Windows runtime evidence is attached.

Ruling: Keep the PR in draft and do not request the named reviewer until real Windows workflow evidence is available — issue #200 requires UI/network evidence and explicitly says CI is not reviewer approval — cost if wrong: review begins after automated tests but before the stated review gate is met.
