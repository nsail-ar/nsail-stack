# The NSail Stack guide

After years of building data applications in .NET, the same work kept coming back with every
feature:

- **One operation, written five times.** A controller action, a client method, a TypeScript
  type, a permission constant and a DI registration — all saying the same thing, all drifting
  apart.
- **Disconnected graphs.** What arrives over HTTP is not tracked by the `DbContext`, and copying
  a DTO and its children onto entities is page after page of assignments.
- **Rules that live in people's heads.** Who may call what, which screen refreshes when, what a
  refusal looks like — remembered instead of enforced.

The Stack's answer is one idea applied everywhere: **an operation is declared once, as a
message, and everything else is derived from it.** The endpoint, the typed client, the
TypeScript, the permission key, the form's validation and its labels all come from the same
type, so they cannot disagree.

This guide explains how that works and why. The binding rules — what a contributor or a coding
agent must follow — are the [doctrine](../plugins/nsail-stack/doctrine); these pages are the
explanation in front of it.

## Start here

- [Getting started](getting-started.md) — a working app from the packages on nuget.org and npm, step by step

## How it works

- [Messages](messages.md) — the message as the contract, and everything derived from it
- [Sending](sending.md) — the pipeline, in-process and remote senders, the ambient unit of work, refusals
- [Events](events.md) — publish and subscribe, UI events, domain events, events pushed to the browser
- [Permissions](permissions.md) — policies over messages: authorization constrains inputs, never outputs
- [Mapping](mapping.md) — writing message graphs onto entities, Detached Mapper's semantics generated at compile time
- [Principles](principles.md) — what the Stack optimizes for, and how it decides when they conflict

## Elsewhere

- [The Sample](../src/Stack/Sample/docs/sample.md) — one API, a Blazor client and a React client
- [The doctrine](../plugins/nsail-stack/doctrine) — every rule, area by area ([routing table](../plugins/nsail-stack/skills/doctrine/SKILL.md))
- [Packages on nuget.org](https://www.nuget.org/profiles/NSail)
- [The Stack by area](../README.md#the-stack-by-area) — each area's packages, registration calls and options
