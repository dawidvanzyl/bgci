# Coding Standards — Shared Workflow Spec (v1.0)

Global workflow rules for the BoardGame Collection Insights (bgci) project. They apply
across the backend, the frontend and `bgg-writer`, unless a stack-specific standard
constrains them. Shared Standards outrank all stack standards (§4).

---

## 0. Issue Conventions

Every issue kind has a required title prefix and label. The matching
`.github/ISSUE_TEMPLATE/*.md` enforces both. Do not deviate from either.

| Issue kind | Title prefix | Label | Template |
|---|---|---|---|
| Epic | `[Backlog]` | `epic: backlog` | `epic-issue.md` |
| Feature sub-issue | `[Feature]` | `type: feature` | `sub-issue.md` |
| Bug | `[Bug]` | `type: bug` | `sub-issue.md` (same shape, swap prefix and label) |
| Tech debt | `[Tech Debt]` | `type: tech-debt` | `tech-debt-issue.md` |

**Read the milestone version from the assigned GitHub milestone's own title**
(`gh issue view {n} --json milestone`). Never parse it from the issue's title text. Epic
and sub-issue titles never carry a milestone tag.

A milestone title is a SemVer version without the `v`, for example `1.1.0` or
`1.2.0-alpha`. It is also the release the milestone ships (§7).

Issues created before this workflow keep their old labels and are not retroactively
changed.

---

## 1. Branching

### 1.1 Branch types

Every branch is tied to a GitHub issue. It is cut from the correct base branch (§1.2),
kept short-lived, and merged only through a PR.

- **Feature / bug / tech-debt:** `{feature|bug|tech-debt}/{issue_number}-{slug}`. The
  prefix comes from the issue's label: `type: feature` → `feature/`, `type: bug` →
  `bug/`, `type: tech-debt` → `tech-debt/`. The slug is kebab-case from the issue title,
  at most 5 words. The name never contains a milestone number. Example:
  `feature/55-sqlite-to-postgres-repository`.
- **Milestone:** `milestone/m{version}`. This is the integration branch for grouped work.
  It receives merges only from feature branches (Mode A). `version` is the epic's assigned
  milestone title (§0). Example: `milestone/m1.1.0`.

### 1.2 Workflow modes

Every change lives on a branch and reaches its target through a PR. Direct commits to
`master` are forbidden. Nobody bypasses the topology.

- **Mode A — milestone-driven:** each feature branch belongs to exactly one milestone.
  The milestone branch is the integration boundary. Topology:
  `{feature|bug|tech-debt}/* → milestone/m{n} → master`. No milestone feature branch
  merges directly into `master`.
- **Mode B — direct-to-master:** for a standalone issue that has no milestone (a small fix
  or a tech-debt item). Topology: `{feature|bug|tech-debt}/* → master`.

---

## 2. Commit Messages

Format: `{type}({scope}): {short description}`.

- **Allowed types:** `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `ci`.
- **Scope** must match an existing folder or module name exactly, for example `api`,
  `game-collection`, `bgg-integration`, `frontend`, `bgg-writer`, `e2e`, `docs`. Do not
  invent scopes, synonyms or abbreviations.
- **Breaking changes** carry `!`, for example `feat(api)!: change game response
  contract`.
- **Hygiene:** imperative mood, no trailing period, subject at most 72 characters, a body
  for non-trivial changes.

Examples: `feat(api): add expansion endpoint` · `fix(bgg-integration): handle empty
search result` · `refactor(frontend): split modal rendering`.

---

## 3. Pull Requests

- **Title:** `{issue_title} (#{issue_number})`. **Body:** a change summary,
  `Closes #{issue_number}`, and the milestone reference if there is one.
- **Targets:** `milestone/m{n}` (Mode A) or `master` (Mode B, and the final milestone
  merge).
- **Creation gate:** do not open a PR until the solution builds, formatting and linting
  pass, and all tests pass.
- **Merge gate:** do not merge a PR until CI passes, tests pass, and the issue is linked.
- **Merge strategy:** feature, bug and tech-debt branches are always squash-merged into
  their target. Milestone branches are always merge-committed into `master`, with no
  squash, so history stays. No other merge strategy is permitted.

---

## 4. Documentation Hierarchy

Precedence: **Shared Standards** (highest) → **Backend / Frontend / bgg-writer / Design
Standards**.

- Shared governs the branching model, commit structure, PR lifecycle, merge rules,
  milestones, workflow modes, versioning and releases.
- Backend (`coding-standards-backend.md`) governs C#, ASP.NET Core, Dapper, MediatR,
  domain and application architecture, data access and backend tests.
- Frontend (`coding-standards-frontend.md`) governs the Vite and ES-module frontend and
  its tests.
- bgg-writer (`coding-standards-bgg-writer.md`) governs the Node.js sidecar.
- End-to-end (`coding-standards-e2e.md`) governs the Playwright suite, the QA stack and the
  mock BGG server.
- Design (`design-standards.md`) governs visual style, components and wording.

On any conflict, Shared wins and the stack document is updated to conform. A stack
document may never introduce another branch type, merge flow, PR requirement or commit
exception.

---

## 5. Acceptance Criteria Test Codes

- **Format:** unit tests (backend and frontend) use `{issue_number}UC{n}`. Playwright E2E
  tests use `{issue_number}IT{n}`. Codes are scoped to an issue's own number, never a
  milestone. In a sub-issue body two numbers can appear. UC codes use **that sub-issue's**
  number. IT codes are copied verbatim from the **epic's** acceptance criteria and carry
  the epic's number.
- One IT code per Playwright spec file or `describe` block, repeated across every G/W/T
  line that block covers.
- Never use `NFC` as a code. Assign a real `UC` or `IT` code.
- No suffixes or variants (`317UC-bug1`, `317UC3a`). A test added during rework takes the
  next free `{issue_number}UC{n}`.
- Codes are opaque literal match strings to the test runners (`dotnet test --filter
  "AC=CODE"`, vitest `--tags-filter="CODE"`, `playwright test --grep "@CODE"`). The
  format carries no meaning to tooling beyond the literal string. The `AC=` prefix exists only
  because xUnit traits are key-value pairs. A vitest tag and a Playwright tag are the bare code.

---

## 6. Code Comments

The code speaks for itself. Names, types and small functions carry the intent. A comment
is the exception, written only when the code cannot say it.

- **Default to none.** Before writing a comment, try renaming or extracting until it is
  unnecessary.
- **Allowed:** a non-obvious *why*. This means an invariant, a security or concurrency
  reason, or a workaround for an external defect (for example a BGG or Cloudflare
  behaviour) that would surprise a competent reader. Keep it to one or two lines. Write
  public-API doc comments only where the project already uses them.
- **Never:** a restatement of what the code does. A story, issue, ruling, decision or
  review reference (`#317`, `R3`, `D4`, `review-1 Blocker 3`). A plan or journal label. A
  description of temporary state or of what a later story will change. That history lives
  in the PR and the journal, not in the code.
- The same applies to test names, suppression justifications and other strings that read
  as prose.

---

## 7. Versioning and Releases

Closing a milestone cuts a release. Version tags drive `.github/workflows/publish.yml`, which
builds and pushes the Docker images and creates the GitHub Release.

- **Version:** SemVer. The milestone title is the version (§0), for example `1.1.0`.
- **Tag:** `v{version}`, for example `v1.1.0`. A pre-release suffix is allowed
  (`v1.2.0-alpha`), as in the existing tags. The tag must match `v*.*.*`.
- **When:** after the milestone PR is merged into `master`, `close-milestone` tags the merge
  commit on `master` and pushes the tag. Nothing else creates a release tag for milestone
  work.
- **No marker tags:** do not create `milestone/m*` tags. A branch and a tag with the same
  name make git refs ambiguous, and the release tag already marks the milestone's end.
- **Order:** a milestone version must be higher than the latest `v*` tag.
- **Immutable:** never move, delete or re-push a published tag. To fix a bad release, ship the
  next PATCH version.
- **Mode B work** (§1.2) is not released automatically. The owner tags a PATCH release by
  hand when wanted, with the same `v{version}` format.
- A push to `master` publishes the `latest` images. A pushed `v*.*.*` tag publishes the
  versioned images and the GitHub Release.
