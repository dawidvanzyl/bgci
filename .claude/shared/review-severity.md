# Shared review conventions

Used by `review-pull-request` and `verify-implementation`. Edit it here and never
inline a copy into either, or they will drift.

## Severity levels

These apply to every finding, whatever produced it: requirements, correctness,
standards or security.

- ❌ **Blocker**: incorrect, incomplete, or violating an explicit requirement,
  constraint or documented standard. It must be resolved before merging.
- ⚠️ **Warning**: a soft concern, such as a missing safeguard, a questionable pattern,
  or something likely to cause problems later.
- ❓ **Question**: ambiguous, underspecified or inconsistent, so it can't be judged
  without clarification. Raise it rather than guessing.
- 💡 **Suggestion**: an out-of-scope observation, or an undocumented style
  preference.

Rules:

- **Every finding cites a source**: an issue section and its requirement text, a doc
  and section, or a file:line. A finding you can't source is a Question or a
  Suggestion.
- Work that `## Out of Scope` excludes is at most a Suggestion. A diff that
  *implements* something listed there is the reverse case, and it is a Blocker.
- Warnings, Suggestions and Questions don't gate, but none is optional. Each one needs
  a disposition from the implementer: actioned, deferred with a reason, or disputed
  with a cited reason. Never file a GitHub tracking issue for a deferred finding;
  raise it with the developer instead.

## Standards docs to read

- `docs/coding-standards.md`: always.
- Backend (`src/` touched): `docs/coding-standards-backend.md` and `src/.editorconfig`.
- Frontend (`frontend/` touched): `docs/coding-standards-frontend.md` and
  `frontend/.editorconfig`.
- bgg-writer (`node/bgg-writer/` touched): `docs/coding-standards-bgg-writer.md`.
- E2E (`e2e/` touched): `docs/coding-standards-e2e.md`.
- Frontend diff that adds or changes a button, form, modal, label or confirmation text:
  also `docs/design-standards.md`. Apply its Section 8 checklist. A "no" on any item is a
  violation. Cite the section (for example `§6.3`) in the finding.

If a doc doesn't exist, note that and skip it. Check every applicable rule against
every file in the diff, and take each rule at face value: if the doc says "always do
X" and the code does Y, that's a violation, whatever the intent.

## Security review

bgci has no security standards document and no `asvs-security-review` skill yet. Check
the diff for the usual problems and report them with `Category: Security`:

- untrusted text (game names, BGG data, URLs) reaching `innerHTML` without `esc()`
- secrets, tokens or credentials in code, config, logs or tests
- raw exception text or stack traces returned to the client
- unvalidated input crossing into the database, a URL or a file path
- a new endpoint, setting or container port that widens what the app exposes

A concern you cannot source from a standard is a Question or a Suggestion.

## Report column rules

Findings tables use `# | file:line | Category | Detail`. Questions use
`# | file:line | Question | Context`.

- **file:line**: for example `Song.cs:42`. Use `—` for a requirement-level finding
  with no single line.
- **Category**: one of Standards, Requirements, Correctness, Contract, Security or
  Design.
- **Detail**: quote the source and explain it concisely.
