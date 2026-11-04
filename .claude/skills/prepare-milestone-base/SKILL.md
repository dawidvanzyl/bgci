---
name: prepare-milestone-base
description: >
  Load this skill when the user says "prepare milestone base",
  "prepare-milestone-base", or "/prepare-milestone-base". Checks out master,
  pulls latest, and creates and pushes a milestone/m{version} branch derived
  from an epic issue.
license: MIT
metadata:
  audience: maintainers
  workflow: git-branch-management
---

## Goal

Create `milestone/m{version}` from the latest `origin/master` and push it. This is the
integration branch for Mode A (milestone-driven development) in `coding-standards.md`.
`prepare-base` protects `milestone/*` branches from deletion.

This skill only creates and verifies a branch. Make no file edits, no commits and no
tags. Don't merge, rebase or rewrite history, except for the confirmed reset in step
3. Keep communication concise, with no emojis.

## Inputs

- `epic_issue_number`: ask "What is the epic issue number?" if it isn't given.

## Procedure

### 1) Derive the milestone version

Run `gh issue view {epic_issue_number} --json title,milestone`.

- If no milestone is assigned, stop and ask the user to assign one on GitHub.
- The version is the milestone's own title (`.milestone.title`), never the epic's title. It must be
  SemVer without the `v`: `^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$`, for example `1.1.0` or
  `1.2.0-alpha` (`docs/coding-standards.md` §0). The branch is `milestone/m{version}`, so `1.1.0`
  becomes `milestone/m1.1.0`.
- If the title doesn't match, stop and ask the user to fix the milestone on GitHub. Do not
  guess a version.
- Check the release tag will be free and ordered. Run `git fetch origin --tags`, then
  `git tag --list "v{version}"` (must be empty) and `git tag --list "v*" --sort=-v:refname`
  (`v{version}` must sort above the newest tag; `docs/coding-standards.md` §7). Otherwise stop
  and tell the user, because `close-milestone-watch` would refuse the tag at the end.

### 2) Clean tree

If `git status --porcelain` isn't empty, report "Working tree has uncommitted changes.
Please commit or stash before continuing." and stop.

### 3) Create or reuse the branch

Run `git fetch origin`. Then check whether the branch exists remotely
(`git ls-remote --heads origin milestone/m{version}`) and locally
(`git branch --list milestone/m{version}`).

**The branch doesn't exist:**

```bash
git checkout -B milestone/m{version} origin/master
git push -u origin refs/heads/milestone/m{version}:refs/heads/milestone/m{version}
```

Push with the fully qualified refspec, so a stray tag of the same name can never make the
push ambiguous.

**The branch exists:** ask the user "Branch milestone/m{version} already exists. Check
it out and hard-reset it to {origin/milestone/m{version} | origin/master if it exists
locally only}?". Use the reset target that applies, and accept only `yes`, `y` or
`confirm`; anything else stops. On confirmation:

1. Run `git switch milestone/m{version}`. Use `switch`, not `checkout`: `switch`
   refuses to detach onto a same-named tag, while `checkout` would do it silently.
2. Run `git reset --hard origin/milestone/m{version}`. If the branch is local-only,
   say so and run `git reset --hard origin/master` instead.

### 4) Verify

Confirm that `git branch --show-current` is `milestone/m{version}` and that
`git ls-remote --heads origin milestone/m{version}` returns it. If either check fails,
report which one and stop. Otherwise report: "Milestone branch **milestone/m{version}**
is active locally and present on origin. Ready for sub-issue implementation."
