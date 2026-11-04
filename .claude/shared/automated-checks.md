# Shared automated checks

The single definition of the build, format and test gauntlet. It matches
`.github/workflows/ci-pr.yml`. `implement-plan` runs it during implementation. This is the
one place the checks run in the automated flow, so `verify-implementation` reviews code
that already builds and passes, and the reviewer and CI do not repeat the work. Edit it
here. Never inline a copy, or callers drift.

Run every check for the scopes in the diff:
- anything under `src/` is **backend**
- anything under `frontend/` is **frontend**
- anything under `node/bgg-writer/` is **bgg-writer**
- anything under `e2e/` is **e2e**

Record pass or fail for each check. Do not install dependencies or tooling. If a command is
unavailable, skip it and note that in the report.

Run each command on its own. Use no shell loops, pipes or `2>&1`. A compound command
matches no permission rule, so under an agent it stops on a prompt. In PowerShell,
redirecting a native command's stderr turns every line into an error record and reports
failure even when the exit code is 0. Standard error is already captured.

## Backend

These are the same commands `ci-pr.yml` runs, so a local pass means what a CI pass means:

```bash
dotnet build src/bgci.slnx
dotnet format src/bgci.slnx --verify-no-changes --severity error
dotnet test src/bgci.slnx
```

- The format check uses `--severity error` because the existing code has naming warnings
  (`IDE1006`) that `dotnet format` cannot fix. Do not drop the flag until those are fixed.
- `dotnet test` on the solution reports results per project. If it ran nothing, report
  "No backend test projects found" instead of pass or fail.

## Frontend

Run these in `frontend/`:

```bash
npm run lint
npm run format:check
npm run build
npm run test
```

There is no `typecheck` script. The frontend is plain JavaScript.

## bgg-writer

Run these in `node/bgg-writer/`:

```bash
npm run lint
```

## E2E (static checks)

Run these in `e2e/`. They do not need the QA stack:

```bash
npm run lint
npm run format:check
npm run typecheck
```

The specs themselves run against the QA stack. Run only the story's IT codes locally, one
`--grep "@{IT_CODE}(?!\d)"` per code. CI runs the full suite.

## Comment labels

Run on the diff's added lines only, case-sensitive, over `src`, `frontend/src`,
`frontend/tests`, `node/bgg-writer/src` and `e2e`:

```bash
git diff origin/{base_branch}... -- src frontend/src frontend/tests node/bgg-writer/src e2e
```

Pattern: `plan|ruling|this story|later story|\bAC\d+\b|\bR\d+\b|\bD\d+\b|\d+UC\d+|#\d{3}|review-\d`

Fix every hit on a comment, test name or justification string before reporting done. A UC
trait value is not a hit.

## Report

List the results under **Automated checks:**. Omit lines for checks that did not apply:

```markdown
**Automated checks:**
- dotnet build: {passed/failed}
- dotnet format: {passed/failed}
- dotnet test: {overall pass/fail, with the per-project counts dotnet reports, e.g. "GameCollection.Domain.Tests: 12/12", or "No backend test projects found"}
- frontend npm run lint: {passed/failed}
- frontend npm run format:check: {passed/failed}
- frontend npm run build: {passed/failed}
- frontend npm run test: {passed/failed}
- bgg-writer npm run lint: {passed/failed}
- e2e npm run lint: {passed/failed}
- e2e npm run format:check: {passed/failed}
- e2e npm run typecheck: {passed/failed}
```

A failing check is a ❌ Blocker. Include the last ~50 lines of its output. Include none for
checks that passed.
