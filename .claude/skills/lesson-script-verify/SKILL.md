---
name: lesson-script-verify
description: Verify one BrickShare lesson script by running every step (all steps except git add, commit, and push) in the current repository. Where reality differs from the script, fix the script in docs/lessons/. Use when the user asks to verify, check, or dry-run a lesson, e.g. "/lesson-script-verify 1.5".
argument-hint: <lesson id, e.g. 1.5>
model: sonnet
context: fork
agent: general-purpose
disable-model-invocation: true
allowed-tools: Read, Glob, Grep, Edit, Write, Bash(git:*), Bash(dotnet:*), Bash(pwsh:*), Bash(ls:*), Bash(cat:*), Bash(mkdir:*), Bash(test:*)
---

# Verify a lesson script

Arguments: `$ARGUMENTS`. The first word is the lesson id.

You run the lesson script the way the author would, in the current repository, then fix the script where it is wrong. You apply the lesson's files and code in the working tree. You do not commit.

## Hard rules

- Read the whole script first: `docs/lessons/<id>-*.md`. If there is no such file, or the id is not in `docs/CoursePlan.md`, stop and say so.
- Run every step except the git writes. Skip `git add`, `git commit`, `git push`, and any command that changes the index or history. Report each skipped command.
- Leave the lesson's changes in the working tree after the run. The author reviews them with `git diff` and commits them.
- The only docs file you may edit is `docs/lessons/<id>-*.md`. Do not touch `docs/Progress.md`, `docs/CoursePlan.md`, or other docs. Mark no lesson done.
- Do not change files the script does not ask for. Do not revert or reformat the author's other uncommitted changes.
- Write code from the script, not from your own improved version. If the script's code does not work, fix the script, then copy the fixed code into the repository.
- Follow the `lesson-script` rules when you edit the script: plain technical English, full content for hand-written files, CLI commands for generated files (see "Generated files" in `.claude/skills/lesson-script/SKILL.md`).

## Step 1. Check the baseline

The working tree may hold the author's in-progress work. Do not require a clean tree.

1. Run `git status --short` and `git branch --show-current`. Note the state for the report.
2. Read the "Result" section's list of added files. If any of them already exists, stop. The lesson is already applied. Report that and ask the author to revert the lesson's files first.

## Step 2. Environment

- Run `powershell` blocks with PowerShell: write each block to a `.ps1` file in the session scratchpad, then run `pwsh -NoProfile -File <that file>` from the repository root. Do not put the `.ps1` inside the repository.
- Run `bash` or `zsh` blocks as written, from the repository root.
- Check the tools the script needs (`dotnet --version`, and `docker info` if the script mentions Testcontainers or Docker). If one is missing, mark the affected steps blocked and continue with the steps that do not need it.

Environment differences are not script errors. Do not edit the script for them. Examples: the operating system, and the `dotnet` and package versions the machine has.

## Step 3. Run the steps in order

For each step in the script:

1. Read its prose. Note what it changes.
2. Apply file changes: a new file gets the full content from its code block. A changed file gets the change the prose describes. If the prose is ambiguous, that is a script gap: fix it (Step 4).
3. Run each command in the step. Skip the git writes listed in the hard rules.
4. Compare the result with the step's "Expected" line: red or green, the failure reason, the test counts, and the output text. Quote the mismatch.

Check generated files too. If the script hand-writes a file that the dotnet CLI can generate (see "Generated files" in the `lesson-script` skill), that is a script error. Fix it by replacing the hand-written block with the CLI command, as the rule requires.

At the end, check the "Result" section:

- The files listed as added or changed must appear in `git status` for the lesson's paths. Ignore the author's other changes.
- The test command and its expected output must match what you saw.

## Step 4. When reality differs from the script

Classify each mismatch before you act.

**A. Script error: fix the script.** Examples: a wrong command or path, an expected output that does not match, code that does not compile against the tree the earlier steps build, a missing step, a wrong file list in "Result", a hand-written generated file, a step whose red test fails for a reason other than the one the script names.

- Edit `docs/lessons/<id>-*.md` with the smallest change that makes the script true.
- Apply the same change in the repository and continue.
- Record the change for the final report.

**B. Environment difference: do not edit.** Record it in the report.

**C. Reality breaks a rule: stop that step and report.** Examples: the code the script asks for violates `CLAUDE.md` or a feature doc, an earlier lesson's code is missing, or the lesson's stated goal cannot be met. Do not rewrite the script to match broken code. Report what you found and continue with steps that do not depend on it.

## Step 5. Report

Reply with:

- The lesson id and the branch it ran on.
- A table: step, result (pass, fail, skipped, or blocked), and the reason for any fail, skip, or block.
- Every script edit: file, step, what changed, and why (category A).
- Environment differences (category B) and stopped steps (category C), each with the reason.
- The final state: whether the lesson's tests are green in the repository. State that the lesson's changes remain uncommitted in the working tree. Do not mark `docs/Progress.md`. The author does that.
- Whether `docs/lessons/<id>-*.md` changed, with `git diff --stat` on that file.
