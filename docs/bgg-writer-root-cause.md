# BGG writer failure: root cause record

Date of the investigation: 2026-11-09.
Test account: `bgci_tester`. Test game: Wingspan, BGG id 266192.
Website calls used for the comparison: `docs/bgg-website-write-calls.md`.

## Summary

The writer fails because Cloudflare blocks every automated browser the writer can start. The writer never gets past the `Just a moment` page, so it never reaches login, add or remove. The cause is not certain for the second failure the owner logged, where login and add worked but the item id lookup returned HTTP 202 five times. That run could not be repeated, because Cloudflare blocks the writer today.

The old `geekcollection.php` add and remove calls still work when a signed-in Chrome sends them. The XML API bearer token is not needed when the call comes from a signed-in page.

Certainty: high for the Cloudflare block. Not certain for the 202 answers.

Stop rule: reached. Cloudflare rejected Chromium and Firefox. See "Stop-rule result".

## Call-by-call comparison

| Step | Writer call today | Website call | Difference |
|---|---|---|---|
| Browser | `playwright-extra` Chromium with the stealth plugin, headless | Chrome, signed in by a person | Cloudflare blocks the writer's browser. The website runs in a normal Chrome. |
| Cloudflare wait | `goto` the home page, wait until the title has no `Just a moment` (30 s) | Not applicable | The title stays `Just a moment...`. The wait times out. |
| Login | `POST /login/api/v1`, JSON `{ credentials: { username, password } }`, headers `Content-Type`, `Origin`, `X-KL-Ajax-Request` | `POST /login/api/v1`, same body, headers `Content-Type`, `Accept`; response 204 | Same endpoint and body. The writer sends two extra headers. Not shown to matter. |
| Add | `POST /geekcollection.php`, form body `action=additem` and more, success is `response.ok` | `POST /api/collectionitem`, JSON body `{ item: { ... } }`, response `{"message":"Item saved"}` | Different endpoint and body. The old endpoint still adds the item (see H2). |
| Item id lookup | `GET /xmlapi2/collection?username&id&own=1`, five tries, reads `collid="..."` | `GET /api/collections?objectid&objecttype=thing&userid`, JSON `items[].collid` | Different endpoint. The writer needs only a user name. The website needs the numeric user id. |
| Remove | `POST /geekcollection.php`, form body `action=delete&collid` | `DELETE /api/collectionitem/{collid}` | Different endpoint and method. The old endpoint still removes the item (see H2). |

The writer treats any HTTP 2xx as success. `geekcollection.php` answers 200 with an empty body for add and remove, so the writer cannot tell a real write from a silent failure.

## Reproduction

The add call was `POST /collection/add` with `{ username: "bgci_tester", bggId: 266192 }`. The test account had 0 items before and after each run.

| Run | Cloudflare wait | Login | Add | Item id lookup | Remove |
|---|---|---|---|---|---|
| Local, Windows, `node src/index.js` | Timeout after 30 s. The writer returned HTTP 500 `page.waitForFunction: Timeout 30000ms exceeded.` | not reached | not reached | not reached | not reached |
| Docker, image `ghcr.io/dawidvanzyl/bgci-writer:latest`, container `bgg-writer-spike`, port 3101 | Same timeout and HTTP 500 | not reached | not reached | not reached | not reached |

Writer log lines in both runs: `launching browser and creating session`, `navigating to BGG homepage to satisfy Cloudflare`, `addToCollection failed: page.waitForFunction: Timeout 30000ms exceeded.`

Because the add never ran, the writer's remove was not exercised through the writer. The remove call was tested from a signed-in Chrome page instead (H2). The spike container was removed after the runs. No production container was touched.

The owner's earlier log (2026-11-05) shows both failures:
- 12:35 to 12:36: the same Cloudflare timeout.
- 19:26 to 19:28: login and add went through, then each of the five item id lookups returned 202, then `Could not resolve collId ... after add`.

## Hypotheses

| Id | Hypothesis | Result | Evidence |
|---|---|---|---|
| H1 | The XML API needs the bearer token | Ruled out for a signed-in page. Not tested inside the writer. | In a signed-in Chrome page, after one add, `xmlapi2/collection?username=bgci_tester&id=266192&own=1` with no token returned 202 on the first call and 200 with the item about 1.5 s later. The call with the bearer token returned 200 at once. The token is not needed. The owner's five 202 answers over about 30 s are not explained. The writer could not be run past Cloudflare to check them. |
| H2 | The login API or `geekcollection.php` changed | Ruled out for add and remove | From the signed-in page, the writer's add (`POST /geekcollection.php`, `action=additem`) returned 200 with an empty body and the item appeared (0 to 1 items). The writer's remove (`action=delete&collid`) returned 200 with an empty body and the item went away (1 to 0). The login call has the same endpoint and body as the website. The website itself uses other endpoints, but the old ones still work today. |
| H3 | Cloudflare blocks the stealth-patched Chromium | Confirmed | The home page answered HTTP 403 with `cf-mitigated: challenge` and the title `Just a moment...` for 20 to 30 s, in the writer and in separate probes. See "Browser". |

Further finding: the add call is not idempotent. Each call creates a new collection item, so a retry after a lost response makes a duplicate. One duplicate was made during the capture and removed.

## Browser

All probes loaded `https://boardgamegeek.com` and watched the page title. The Chrome used by the owner, from the same machine and network, reaches the site without a challenge.

| Environment | Browser | Result |
|---|---|---|
| Docker (Linux), headless | Chromium with stealth (the writer today) | 403, challenge, `Just a moment...` |
| Docker, headless | Chromium without stealth | 403, `Attention Required! \| Cloudflare` |
| Docker, headless | Firefox | 403, challenge, `Just a moment...` |
| Local (Windows), headless | Firefox | 403, challenge, `Just a moment...` |
| Local, headless | Chromium without stealth | `Attention Required! \| Cloudflare` |
| Local, headed (visible window) | Chromium with stealth | 403, challenge for 30 s |

The evidence supports neither Chromium nor Firefox. Every Playwright browser was blocked. The earlier log of 2026-11-05 at 19:26 shows that the challenge passed at least once, so the block can depend on time or network.

The README and `docs/coding-standards-bgg-writer.md` were not changed.

## Fix direction

No fix is possible at the call level until the Cloudflare block is solved. Changing endpoints does not help, because the writer cannot load the site.

If the owner finds a way past Cloudflare, the writer should change as follows:
- Treat a write as done only when the website's own read call confirms it. After add or remove, call `GET /api/collections` for the game and check that an item is there or gone.
- Use `GET /api/collections?objectid={bggId}&objecttype=thing&userid={userId}` to get the `collid`. It needs no token. It needs the numeric user id. The writer has the user name only. The id is available from `GET /api/users/current` in a signed-in session.
- Prefer the website's `POST /api/collectionitem` and `DELETE /api/collectionitem/{collid}` over `geekcollection.php`, because the website uses them and the old endpoint answers an empty 200 that proves nothing.
- Check that an add did not already make the item before it adds, to avoid duplicates.
- Keep the XML API out of the write path, or send the bearer token if it stays.

## Stop-rule result

Cloudflare rejected Chromium (with and without stealth, headless and headed) and Firefox, in Docker and locally. The approach of driving a browser from the writer is blocked on this network today. The owner must decide how to go on before #89 starts. Options for that decision are not part of this record.

## Test-account state

Final website check on 2026-11-09: the test account `bgci_tester` lists 0 items. Checked with `GET /xmlapi2/collection?username=bgci_tester` (HTTP 200, 0 items) and `GET /api/collections` for game 266192 (0 items). The account had 0 items before the spike.

## Documentation drift

`README.md:324` says the writer fills in and submits the BGG login form. The code logs in with the JSON login API (`POST /login/api/v1`) from inside the page. The README fix is part of #89.
