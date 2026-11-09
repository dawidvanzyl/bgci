# bgg-writer Coding Standards

> Workflow, branching, commit, PR and comment rules: `coding-standards.md`. Those rules outrank this document.

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

---

## BGG website calls

Source: captured from the BoardGameGeek website in Chrome, signed in with the test account, by recording the page's own requests.
Capture date: 2026-11-09.
Test game: Wingspan, BGG id 266192.

BGG can change these calls at any time. They are not a public API. Check them again before you rely on them.

Values that are secret are shown as placeholders such as `{SESSION_ID}`. No password, cookie value or token is in this section.

All calls go to `https://boardgamegeek.com`. The page makes them with XHR from a signed-in browser session.

### Login

| Field | Value |
|---|---|
| Method | `POST` |
| Endpoint | `/login/api/v1` |
| Request headers | `Content-Type`, `Accept` |
| Body | JSON. The field names are in the note below. |
| Response that shows success | HTTP 204. The response has no body. |

Note on the body: the developer did not open the login payload, because it holds the password. The owner reads the field names in Chrome's Network view. Owner-reported structure: `{ "credentials": { "username": "{USERNAME}", "password": "{PASSWORD}" } }`. There are two fields, both inside one `credentials` object, and no other fields.

The website sends only `Content-Type` and `Accept`. It does not send `X-KL-Ajax-Request` or `Origin` on the login call.

### Add

| Field | Value |
|---|---|
| Method | `POST` |
| Endpoint | `/api/collectionitem` |
| Request headers | `Accept`, `Content-Type: application/json`, `X-KL-Ajax-Request: Ajax_Request` |
| Body | JSON, see below |
| Response that shows success | HTTP 200 with the body `{"message":"Item saved"}`. The body has no `collid`. |

Body, with the game id and name as example values:

```json
{
  "item": {
    "collid": 0,
    "pp_currency": "USD",
    "cv_currency": "USD",
    "objecttype": "thing",
    "objectid": "266192",
    "status": { "own": true },
    "objectname": "Wingspan",
    "acquisitiondate": null,
    "invdate": null
  }
}
```

`collid: 0` means a new item. The same call, sent twice, makes two collection items for the same game.

### Collection item id lookup

The website reads the owned item after an add. This call gives the `collid`.

| Field | Value |
|---|---|
| Method | `GET` |
| Endpoint | `/api/collections` |
| Query fields | `objectid` (the BGG id), `objecttype=thing`, `userid` (the numeric BGG user id of the signed-in account) |
| Request headers | `Accept`, `X-KL-Ajax-Request: Ajax_Request` |
| Response that shows success | HTTP 200 with JSON `{ "items": [ { "collid": "{COLLID}", "objectid": "266192", "objecttype": "thing", "status": { "own": true }, ... } ] }`. `items` is an empty list when the game is not in the collection. |

The `collid` is a string of digits.

The add drawer also calls `GET https://api.geekdo.com/api/collections` (header `Accept`) for item details. It is not needed for a write.

The XML API call `GET /xmlapi2/collection?username={USERNAME}&id={bggId}&own=1` is not used by the website to find the item. Called from the signed-in page with no token, it returns HTTP 202 on the first call after an add, and HTTP 200 with the item from about 1.5 seconds later.

### Remove

| Field | Value |
|---|---|
| Method | `DELETE` |
| Endpoint | `/api/collectionitem/{collid}` |
| Request headers | `Accept`, `X-KL-Ajax-Request: Ajax_Request` |
| Body | none |
| Response that shows success | HTTP 200 with the body `{"message":"Item removed from collection"}`. |

In the website, this call runs after the user picks Edit, then Delete from Collection, then confirms Yes, Delete.

### Cookies

The session cookies are HttpOnly. Chrome did not show them to the page script, so their names were not captured. The writer relies on the cookies that the browser keeps after the login call, as the website does. The cookies that the page script can see are only analytics and consent cookies: `_ga`, `__gads`, `__gpi`, `__eoi`, `FCCDCF`, `_ga_*` and `FCNEC`. They are not needed for a write.

If the exact session cookie names are needed, read them in Chrome DevTools under Application, Cookies, and replace each value with a placeholder such as `{SESSION_ID}`.
