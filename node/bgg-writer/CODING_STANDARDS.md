# bgg-writer Coding Standards

## Purpose

`bgg-writer` is a single-responsibility Node.js sidecar service. It exists solely to write to the BGG collection API using a real Firefox browser (via Playwright) to bypass Cloudflare bot protection. It exposes two HTTP endpoints consumed by the main .NET API.

---

## Module Structure

- **One responsibility per file.** `index.js` owns Express wiring only. `bggWriter.js` owns all Playwright and BGG interaction logic.
- Do not add controllers, routers, or service subdirectories unless the service meaningfully grows beyond two endpoints.
- If a third responsibility emerges, create a new file — do not expand existing files.

---

## Formatting

- Tabs, not spaces.
- Single quotes for strings.
- Semicolons required.
- `'use strict'` at the top of every file.

---

## Configuration

- All secrets and environment-specific values come from environment variables only.
- Never hardcode credentials, URLs, or ports.
- Document all required env vars in `.env.example`.
- Read env vars at startup; do not read them deep inside functions.

---

## Error Handling

- All Playwright operations must be wrapped in `try/finally` to ensure the browser is always closed.
- Throw `Error` instances with descriptive messages — callers (`index.js`) are responsible for translating to HTTP responses.
- Do not swallow errors silently. Log with `console.error` before re-throwing or responding with 500.
- Non-fatal warnings use `console.warn`. Informational steps use `console.log`.

---

## Playwright Conventions

- Always use `firefox` — do not use Chromium or WebKit.
- Launch a fresh browser per request. Do not maintain a persistent browser context across requests (simplicity over performance at this scale).
- Always close the browser in a `finally` block — never rely on process exit to clean up.
- Use `page.evaluate()` for all `fetch()` calls to `geekcollection.php` — this ensures requests originate from within the authenticated browser context and pass Cloudflare validation.
- Do not navigate to pages unnecessarily. Only load what is required to complete the operation.

---

## HTTP Layer (`index.js`)

- Validate required fields and return `400` before calling any Playwright logic.
- Return `200` with a JSON body on success.
- Return `500` with `{ error: message }` on failure.
- Do not put business logic or Playwright calls directly in route handlers — delegate entirely to `bggWriter.js`.

---

## Dependencies

- Keep dependencies minimal: `express`, `playwright`, `dotenv`.
- Do not add an ORM, database client, or queue library — this service has no persistence.
- Pin major versions in `package.json`.

---

## Debugging

To run locally with the Playwright Inspector (visible browser + step-through):

```bash
PWDEBUG=1 node src/index.js
```

To attach a Node.js debugger (VS Code or similar):

```bash
node --inspect src/index.js
```

Then attach via VS Code using the "Node.js: Attach" launch configuration pointed at `localhost:9229`.
