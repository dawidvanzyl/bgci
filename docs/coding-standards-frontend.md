# Frontend Coding Standards

Conventions for the bgci frontend: vanilla JavaScript ES modules, HTML and CSS, bundled by
Vite.

> Workflow, branching, commit, PR and comment rules: `coding-standards.md`. Those rules
> outrank this document. Visual and wording rules: `design-standards.md`. Formatting is
> governed by `.editorconfig`, and by ESLint and Prettier once configured. This document
> does not repeat tool-enforced rules.

---

## 1. Project Structure

```text
frontend/
├── index.html          # Static page shell: header, toolbar, grid, modals
├── public/
│   ├── style.css       # All styles (design tokens as CSS variables)
│   ├── config.js       # Runtime config (untracked); config.js.example is the template
│   └── bgg-logo.svg
└── src/
    ├── main.js         # Entry point: DOM wiring and event binding only
    ├── state.js        # Shared mutable state and its setters
    ├── api.js          # The only module that calls fetch
    ├── render.js       # Builds the game grid / cards from state
    ├── modal.js        # Add, edit, info and confirm dialogs
    ├── bgg.js          # BGG search and availability UI
    ├── expansions.js   # Expansions tab
    ├── sort.js         # Multi-level sort
    └── helpers.js      # Small pure utilities and button-loading helpers
```

- One responsibility per module. Add a new module before a module grows a second
  responsibility.
- Only `api.js` calls `fetch`. Every other module goes through it.
- `main.js` wires events and calls the other modules. It holds no feature logic.
- State lives in `state.js`. A module changes shared state only through that module's
  exported setters.

## 2. Language and Style

- ES modules only. Use named exports. Use no globals other than the documented
  `window` config.
- Tabs for indentation, single quotes, semicolons. (`bgg-writer` has its own rules.)
- Use `const` by default. Use `let` only when the binding changes. Never use `var`.
- Look up DOM elements once at module top, as the existing modules do. Do not repeat
  `document.getElementById` inside hot paths.
- Build user-supplied text into the DOM with `textContent`. Never assign untrusted text
  (game names, BGG data, URLs) to `innerHTML`.
- Handle every `fetch` failure. Show the user a message through the existing error
  helpers. Never leave a rejected promise unhandled.

**Known deviations in the current code.** Do not copy them. Fix them when touching the
code, or file a tech-debt issue.

- `main.js` calls `fetch` for `POST /sync/bgg`. It belongs in `api.js`.
- Several modules (`render.js`, `modal.js`, `bgg.js`, `expansions.js`, `sort.js`) build
  markup with `innerHTML` template strings. There is no HTML-escape helper, so game names
  and BGG text reach the DOM unescaped.

## 3. Testability

New logic is split so it can be unit tested without a browser.

- Put calculations (sorting, filtering, formatting, mapping API data) in pure functions.
  They take values and return values. They do not read the DOM or `state.js` directly.
- Keep DOM code thin. It calls the pure function and applies the result.
- Unit tests use vitest, with `{issue_number}UC{n}` codes (see `coding-standards.md` §5).
  End-to-end behaviour is proven by Playwright in `e2e/`.

## 4. CSS

- Colours, radius and shadow come from the CSS variables in `:root`
  (see `design-standards.md` §2). Do not hard-code a colour that a variable covers.
- Use class selectors. Use no IDs for styling and no inline `style` attributes, except to
  toggle visibility through the `hidden` class or attribute.
- Name modifier classes with the existing pattern: `btn-primary`, `game-grid--large`.

## 5. Configuration

- `public/config.js` is local and untracked. Every key it reads is listed in
  `config.js.example`.
- Never put secrets in the frontend.
