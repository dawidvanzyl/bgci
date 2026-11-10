---
name: close-milestone
description: >
  Load this skill when the user says "close milestone", "close-milestone", or
  "/close-milestone". Creates the milestone-to-master PR, launches the
  close-milestone-watch command, and orchestrates the end of a milestone cycle.
license: MIT
metadata:
  audience: maintainers
  workflow: github-issues-pr
---

## Role

Close out a completed milestone: verify it, open the PR into `master`, get CI green,
then hand off to `close-milestone-watch`, which releases the milestone as `v{version}`.

This PR gets **no second review pass**. Every story in it was already reviewed and
tested, and re-reviewing the accumulated diff would reopen settled decisions. CI is the
only thing still watching it, so it must be green before the hand-off.

You make no file edits and no commits. A fix the owner approves goes to a `developer`
subagent, under the same delegation rule that holds for the whole run. Keep
communication concise, with no emojis.

## Inputs

- `epic_issue_number`: ask for it if it isn't given.

## Procedure

### 1) Read context

- Run `gh issue view {epic_issue_number} --json title,body,milestone`.
- Take `milestone_title` from `.milestone.title`. If no milestone is assigned, stop
  and ask the user to assign one. Never parse the epic's own title.
- `milestone_number` is `milestone_title`. It must be SemVer without the `v`
  (`^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$`, for example `1.1.0`; `docs/coding-standards.md`
  §0). If it isn't, stop and ask the user to fix the milestone on GitHub.
- `milestone_branch` is `milestone/m{milestone_number}`.
- Collect every IT code line from the epic's `<!-- AC_START -->` block.
- Get `OWNER` and `REPO` from `git remote get-url origin`.

### 2) Validate prerequisites

Stop and report if any of these fails:
- `git branch --show-current` returns `milestone_branch`.
- `git status --porcelain` is empty.
- Every sub-issue is `CLOSED`. List any that aren't. Query:
  ```
  gh api graphql -f query='{ repository(owner: "OWNER", name: "REPO") {
    issue(number: EPIC_NUM) { subIssues(first: 50) {
      nodes { number title state labels(first: 10) { nodes { name } } }
    } } } }'
  ```
- The release tag is free and ordered: run `git fetch origin --tags`, then
  `git tag --list "v{milestone_number}"` (must be empty) and
  `git tag --list "v*" --sort=-v:refname` (`v{milestone_number}` must sort above the newest
  tag; `docs/coding-standards.md` §7). Otherwise stop, because `close-milestone-watch`
  would refuse the tag at the very end.

### 3) Verify IT codes

IT codes are proven by Playwright alone. Check each code.

1. **Does the spec exist?** Run `grep -rn "@{IT_CODE}" e2e/features --include="*.spec.ts"`.
   A green pipeline can't tell you this, because a code with no spec runs nothing and
   breaks nothing. No match means ❌ FAIL.
2. **Does it pass?** Prefer CI on the branch head. Find the push run for the head SHA and read
   its `E2E` job:
   ```
   gh run list --workflow CI --branch {milestone_branch} --commit {head_sha} --json databaseId,status
   gh run view {run_id} --json jobs --jq '.jobs[] | select(.name == "E2E") | .conclusion'
   ```
   Confirm the SHA is `git rev-parse HEAD`: a run on a superseded commit proves nothing. If the
   job concluded `success`, every code whose spec exists passes. If the job is still running,
   has no run, or failed, verify locally (and read the log of a failed job first):
   - Bring the stack up if needed:
     `curl --silent --fail http://localhost:15100/api/config || docker compose --profile qa up --build -d`
   - From `e2e/`, run `npx playwright test --grep "@{IT_CODE}(?!\d)"` for each code.

Also check that no unit test carries an IT code. Any hit is a ❌ Blocker and never counts as
coverage: a unit test carrying an IT code makes a code look proven while asserting far less.

```bash
grep -rE '\[Trait\("AC", "[0-9]+IT[0-9]+"\)\]' src/ --include="*.cs"
grep -rE '[0-9]+IT[0-9]+' frontend/tests --include="*.test.js"
```

Then re-fetch the epic body and tick `- [x]` for each passing code inside the
`AC_START` block. Write the body only if something changed. Stop with "Milestone
{milestone_number} acceptance criteria partially passing: {n}/{m}. Failing codes:
{list}." unless every code passes and there are no blockers.

### 4) Push and create PR

Run `git fetch origin master`. If `git rev-list --count HEAD..origin/master` is
greater than 0, ask the user: "milestone/m{milestone_number} is {n} commit(s) behind
origin/master. Merge or rebase master into this branch before creating the PR, or
confirm you want to proceed anyway (conflicts, if any, will surface on GitHub)."
Only `yes`, `y` or `confirm` lets you continue.

Push with the fully qualified refspec, so a stray tag of the same name can never make the
push ambiguous:

```
git push origin refs/heads/milestone/m{milestone_number}:refs/heads/milestone/m{milestone_number}
gh pr create --base master --head milestone/m{milestone_number} \
  --title "Milestone {milestone_number}" \
  --milestone "{milestone_title}" \
  --body "## Summary\n\nMilestone {milestone_number}. Merging it releases v{milestone_number}.\n\nCloses #{epic_issue_number}."
```

Capture the PR number from the output.

### 5) CI gate

1. Wait with `gh pr checks {pr_number} --watch`.
2. **Green:** go to step 6.
3. **Red:** read the failing job's log (`gh run view {run_id} --log-failed`) and report it to
   the owner: the job, the failing step and the first error. Every story passed its own
   review and tests, so a failure here means the combined milestone broke something no single
   story did. Do not fix it yourself and do not re-run to hope. With the owner's go, spawn a
   background `developer`. The brief is the failing log, `base_branch: milestone/m{milestone_number}`
   and a `journal_dir`. The fix commits directly to the milestone branch, because there is no
   story to attach it to. The developer must read the surrounding code before changing it,
   since the code may come from a story closed weeks ago.
4. The fix's push re-runs CI. Go back to 1.

**CI is the regression gate, so never proceed while it's red.** You run nothing yourself:
`ci-pr.yml` covers the backend, frontend, bgg-writer and full Playwright suite on every push.
Never disable, skip or loosen a check to get a green result.

### 6) Hand off

Run `/close-milestone-watch` with `$1` set to the PR number and `$2` set to the
milestone version. It waits for the PR to be merged (a merge commit, never a squash;
`docs/coding-standards.md` §3), then closes the milestone, deletes the branch, runs
`prepare-base` and tags `v{milestone_number}` on `master`, which starts the release. Relay
its final summary as this workflow's conclusion, and don't add your own.
