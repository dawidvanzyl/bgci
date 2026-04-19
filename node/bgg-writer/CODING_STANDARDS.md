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

- Wrap individual `Page` operations in `try/finally` to ensure pages are always closed. Do not close the `Browser` or `BrowserContext` in error handling — those are managed by the session lifecycle described in the Playwright Conventions section.
- Throw `Error` instances with descriptive messages — callers (`index.js`) are responsible for translating to HTTP responses.
- Do not swallow errors silently. Log with `console.error` before re-throwing or responding with 500.
- Non-fatal warnings use `console.warn`. Informational steps use `console.log`.

---

## Playwright Conventions

- Always use `firefox` — do not use Chromium or WebKit.
- Maintain a single module-level `BrowserContext` reused across requests. Launch the browser and log in once on first use; do not re-launch unless an authenticated request is rejected with a 401/403.
- All session initialisation and invalidation must be serialised through a mutex (promise-chain lock) to prevent concurrent requests from racing to create or destroy the browser.
- Close individual `Page` instances in a `finally` block after each request. Do not close the `Browser` or `BrowserContext` except during session invalidation.
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
