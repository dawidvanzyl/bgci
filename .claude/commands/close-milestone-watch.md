---
description: Wait for milestone PR to merge, then close milestone, prepare-base, and release
subtask: true
---

## Inputs

- `$1` is the PR number of the milestone → `master` PR. Ask "What is the milestone PR
  number?" if it's missing.
- `$2` is the milestone version, the milestone's title, e.g. `1.1.0` or `1.2.0-alpha`. Ask
  "What is the milestone version?" if it's missing.

Call them `PR_NUMBER` and `MILESTONE_NUMBER`. The branch is `milestone/m$MILESTONE_NUMBER` and
the release tag is `v$MILESTONE_NUMBER` (`docs/coding-standards.md` §0 and §7).

## Watch loop

Run `gh pr view $PR_NUMBER --json state --jq '.state'` every 30 seconds (`sleep 30`):

- **`MERGED`:** post "PR #$PR_NUMBER merged. Proceeding with milestone close, branch
  deletion, and the v$MILESTONE_NUMBER release tag. Interrupt now if this is unexpected." and go
  to **On merge**.
- **`CLOSED`:** post "PR #$PR_NUMBER was closed without merging. Aborting milestone
  close." and exit.
- **Still open after 20 checks (~10 minutes):** ask "PR #$PR_NUMBER has not merged
  after ~10 minutes. Continue waiting? (yes/no)". `yes`, `y` or `confirm` resets the
  count. Anything else posts "Stopping watch. Run
  `/close-milestone-watch $PR_NUMBER $MILESTONE_NUMBER` again to resume." and exits.

## On merge

### 1) Close the GitHub milestone

`MILESTONE_NUMBER` is the milestone's title. It is **not** the GitHub API number, and the two
diverge (`1.1.0` can be API number `7`). Read both from the PR:

```
gh pr view $PR_NUMBER --json milestone --jq '.milestone.title, .milestone.number'
```

If the PR has no milestone, or its title is not `$MILESTONE_NUMBER`, post the mismatch and stop.
The release tag comes from that title, so a wrong one ships the wrong version. Then close it:

```
gh api repos/{owner}/{repo}/milestones/{that number} -X PATCH -f state=closed
```

If the PATCH fails, post the error and stop. Never fall back to `$MILESTONE_NUMBER` as the API
number. Post "Milestone $MILESTONE_NUMBER closed on GitHub."

### 2) Prepare master

Invoke `prepare-base` with `base_branch = master` and `mode = interactive`. Pass both
rather than asking for them. Use interactive mode on purpose: every story branch has
just finished, which makes this the one point where the stale-branch cleanup is worth
running, and subagent mode skips it.

### 3) Delete the milestone branch

```
git branch -d milestone/m$MILESTONE_NUMBER
git push origin --delete milestone/m$MILESTONE_NUMBER
```

The `guard-destructive` hook intercepts both commands. In your own session it asks
for confirmation, so confirm and continue. Under an agent it denies them. In that
case don't rephrase or route around them; post both commands for the user to run,
note the deletion as outstanding, and continue.

- If `-d` fails because the branch isn't fully merged, post the error and stop.
  **Never use `-D`**: a branch git doesn't consider merged is a reason to look, not
  to force.
- If the remote delete fails, note it and continue.

### 4) Release

Pushing the tag is what releases: `publish.yml` runs on `v*.*.*`, calls `ci-pr.yml`, and
publishes the versioned images and the GitHub Release only if CI passes.

First check the tag is allowed. Stop and post the problem if either check fails:

```
git fetch origin --tags
git tag --list "v$MILESTONE_NUMBER"
git tag --list "v*" --sort=-v:refname
```

- The first list must be empty. A tag that exists is never moved or re-pushed.
- `v$MILESTONE_NUMBER` must sort above the newest `v*` tag in the second list
  (`docs/coding-standards.md` §7).

Then tag and push:

```
git tag v$MILESTONE_NUMBER origin/master
git push origin refs/tags/v$MILESTONE_NUMBER
```

Tag `origin/master` rather than `HEAD`, because `prepare-base` may have left you off
master if another worktree holds it. Push the fully qualified tag ref.

Post: "Milestone $MILESTONE_NUMBER complete. PR #$PR_NUMBER merged to master, milestone closed on
GitHub, milestone branch deleted {or: deletion outstanding — see above}, tag v$MILESTONE_NUMBER
pushed and the release workflow started, master is current and ready for the next milestone."
