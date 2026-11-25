---
name: lesson-script
description: Write the script for one BrickShare course lesson as a single Markdown file in docs/lessons/. The script has a short summary, the problem the lesson solves, and the source code step by step. Use when the user asks for a lesson script, e.g. "/lesson-script 2.10".
argument-hint: <lesson id, e.g. 2.10>
model: haiku
context: fork
agent: general-purpose
disable-model-invocation: true
allowed-tools: Read, Glob, Grep, Write, Bash(ls:*), Bash(git log:*), Bash(git ls-files:*)
---

# Write a lesson script

Lesson id: `$ARGUMENTS`

You write the script for this one lesson. The author reads the script and types the code into the repository by hand.

## Hard rules

- Write exactly one file: `docs/lessons/<id>-<slug>.md`. Example: `docs/lessons/2.10-inventory-code.md`. The slug is a short kebab-case form of the lesson title.
- Do not create, edit, or delete any other file. Do not touch source code, `docs/Progress.md`, `docs/CoursePlan.md`, or other docs. All code goes into the Markdown file only.
- Do not build, run, or format code.
- If the lesson id is missing or is not in `docs/CoursePlan.md`, stop and say so. Do not guess.
- If the file already exists, read it and rewrite it in full.

## Step 1. Gather context

Read these before you write anything:

1. `CLAUDE.md`: code conventions, DDD and TDD rules, architecture rules. The script code must follow all of them.
2. `docs/CoursePlan.md`: find the lesson row. Note its title, its features (F01 = `docs/features/01-*.md`, and so on), and the lessons before and after it.
3. Each feature file the lesson links to in `docs/features/`. Business rules come from here only.
4. The matching sections of `docs/Architecture.md` (the service section, plus 7.1 and 7.2 for DDD and TDD).
5. Earlier lesson scripts in `docs/lessons/`, at least the previous one. Reuse their names, folders, and types.
6. The current source tree (`git ls-files`, then read the files the lesson changes). The author may have changed the code. The code in the repository wins over earlier scripts.

Scope: cover only what the lesson title promises. Do not build things that belong to later lessons. If a later lesson needs a hook, leave it out and let that lesson add it.

## Step 2. Write the script

Use this structure:

````markdown
# Lesson <id>. <Title>

Features: [F01](../features/01-catalog-management.md) (omit the line if the lesson has none)
Previous: [<id>](<file>.md) · Next: <id> <title>

## Summary

Two to four sentences: what we build and what works at the end.

## The problem

What is wrong or missing before this lesson, and why it matters for BrickShare.
Quote or link the business rules from docs/features/ that drive the lesson.
Name the concept or pattern we introduce and why it fits here.

## Step 1. <Short imperative title>

One or two sentences: why this step.

`path/to/File.cs`

```csharp
// code
```

Run: `dotnet test ...` → expected result (red, with the reason, or green).

## Step 2. ...

## Result

What now works. The test command and the expected green output.
A short list of files added or changed.
````

### Step rules

- Follow TDD: a failing test first, then the simplest code that passes, then a refactor step if one is needed. Say "red" or "green" after each test run, and why.
- Test names describe behavior (`Pickup_fails_when_deposit_is_not_secured`). Use xUnit v3 and Shouldly. Never FluentAssertions.
- One step changes one thing. Keep steps small enough to type in a few minutes.
- Every code block starts with its repository-relative file path on the line above.
- New file: show the full content. Changed file: show the full changed member or block, with enough context lines to find it, and say what replaces what.
- Code must compile against the current tree and match the project conventions: Minimal APIs, vertical slices, central package management (versions only in `Directory.Packages.props`), `TimeProvider`, `long` money in minor units, RFC 9457 `ProblemDetails`, and authorization on every endpoint group.
- Include shell commands the author must run (`dotnet new`, `dotnet add package`, `dotnet ef migrations add`, `dotnet user-jwts`) as `powershell` blocks.
- Do not add explanations as code comments. Explain in the prose around the block.
- Plain technical English: short sentences, active voice, one term per concept.

## Step 3. Check and report

Before you finish, reread the script and check:

- Every business rule in the code traces to a feature doc.
- No step uses a type or file that an earlier step or the current tree does not define.
- Nothing from a later lesson slipped in.

Then reply with the file path and a two-line summary. Mention any open question or doc gap you found, but do not fix docs yourself.
