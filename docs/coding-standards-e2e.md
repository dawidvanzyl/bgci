# End-to-End Test Standards

Conventions for the Playwright suite in `e2e/`.

> Workflow, branching, commit, PR and comment rules: `coding-standards.md`. Those rules
> outrank this document. Selector and wording facts come from `frontend/index.html` and
> `design-standards.md`.

---

## 1. Layout

```text
test-data/
├── bgg-games.json         # The standard game data set, shared with the live suite
└── bgg-live-test-accounts.json  # Usernames the live BGG write test may use
e2e/
├── mock-bgg/              # Mock of BGG: the XML API, the login and the collection write calls
├── playwright.config.ts
├── global-setup.ts        # Waits until the stack is up and the API sees the mock BGG
├── fixtures/              # base.ts (test + auto fixtures), mockBgg.ts (mock control client)
├── pages/                 # One page object per screen
└── features/              # Specs, one folder per feature
```

- TypeScript, ESLint and Prettier. Run `npm run lint`, `npm run format:check` and
  `npm run typecheck` from `e2e/`.
- Specs import `test` and `expect` from `fixtures/base`, never from `@playwright/test`.

## 2. Running the suite

```bash
docker compose --profile qa up --build -d
```

```bash
cd e2e && npm ci && npx playwright install chromium && npm run test:e2e
```

```bash
docker compose --profile qa down -v
```

- The stack is the `qa` profile of the root `docker-compose.yml`. It runs a PostgreSQL service with no
  volume, so every `up` starts with an empty database. Production is `docker-compose.prod.yml`.
- Ports: web `QA_WEB_PORT` (default 15100), mock BGG `QA_MOCK_PORT` (default 15103),
  jobs dashboard `QA_JOBS_DASHBOARD_PORT` (default 15104).
- To run one story's tests, filter by its code: `npx playwright test --grep "@{IT_CODE}"`.
  Run the whole suite in CI, not locally.
- The suite shares one database, so it runs with one worker. Do not enable parallel runs.

## 3. Mock BGG

`mock-bgg` replaces BoardGameGeek. The API and the jobs service reach it through `Bgg__BaseUrl`, the site root.
The jobs service logs in with `Bgg__Username` and `Bgg__Password`. Tests never call the real BGG.

- It serves `xmlapi2/search`, `xmlapi2/thing` and `xmlapi2/collection` as BGG XML, built from the standard game data set.
  The data set is `test-data/bgg-games.json` at the repository root. Mock BGG and the live suite
  both read this one file. Neither keeps a game list of its own.
- The file is one JSON object with one key, `games`, an array of entries. An entry has these fields.
  All are required unless marked optional.

  | Field | Type | Meaning |
  |---|---|---|
  | `id` | integer | Real BGG id. Unique in the data set. |
  | `name` | string | Primary name, as mock BGG serves it. |
  | `year` | integer | Year published. |
  | `description` | string | Description text. |
  | `minPlayers`, `maxPlayers` | integer | Player count range. |
  | `playTime`, `minPlayTime`, `maxPlayTime` | integer | Minutes. |
  | `minAge` | integer | Minimum age. |
  | `rating` | number | BGG average rating. It is a community value, not a user rating. |
  | `weight` | number | BGG average weight. It is a community value, not a user weight vote. |
  | `categories`, `mechanics`, `designers`, `artists`, `publishers`, `subdomains` | string[] | Link names. May be empty. |
  | `expansions` | `{ "id": integer, "name": string }[]` | Expansions of this game. May be empty. |
  | `expansionOf` | integer, optional | Only on an expansion entry: the BGG id of its parent game. |
  | `liveWritable` | boolean | `true` when the live suite may add this game to the test account and remove it again. |

- The ids are real BGG ids. The other values are test values and can differ from the current values
  on BGG. A consumer of the real BGG matches a game by `id` only, never by name or details.
- `liveWritable` applies to its own entry only. A writable parent does not make its expansions
  writable. Mark a game writable only when the test account does not own it outside the live suite.
- To add a game:
  1. Take the real BGG id from boardgamegeek.com. It must not be in the data set already.
  2. Fill every required field.
  3. For an expansion, set both sides of the link: the expansion's `expansionOf` and an entry in
     the parent's `expansions`.
  4. Set `liveWritable` to `false` unless the owner approves a live write for the game.
- To add a new kind of data, such as plays, ratings or weight votes, add a new optional key to the
  entries. Never rename or remove a key, and never give a key a new meaning. A consumer ignores keys
  it does not use. Give user-entered data its own key name, for example `plays`, `userRating` or
  `weightVote`, never `rating` or `weight`.
- The mock BGG image holds a copy of the data set made at build time. After a change to the file, run
  `docker compose --profile qa up --build -d` so the image gets it. The build reads the file through
  a second build context, so it needs Docker Compose 2.17 or later.
- It also serves the BGG write calls: the login (`login/api/v1`), the add (`api/collectionitem`) and the
  remove (`api/collectionitem/{collId}`). A login gives a session, and an add or remove needs a live session.
  An add does not change the collection that the sync reads. Only the collection item id lookup
  (a `collection` request with `id`) sees the items that adds created. That lookup also sees the items from
  `setCollection([...])`, until a remove records their collection item id.
- Read what it did with `mockBgg.report()`: the accepted writes, the accepted and rejected logins, and
  the rejected writes. `mockBgg.acceptedWrites()` returns only the accepted adds and removes, in order.
- Steer it from a spec through the `mockBgg` fixture: `setAvailable(false)`,
  `setCollection([...])`, `setLoginsRejected(true)`, `expireSessions()`, `reset()`.
  `setLoginsRejected(true)` makes every login answer 400 and every add and remove answer 401.
  `expireSessions()` makes the next add or remove with an old session answer 401, while a new login works.
  `reset()` clears both controls and every control listed below.
- It also holds, fails and records writes. A write is an add or a remove. Logins and the collection item lookup are not writes.
  - `holdWrites('all')` makes every write with a valid session wait until `releaseWrites()`. `holdWrites('next')` holds only the next one.
    `releaseWrites()` answers every held write as it would have been answered without the hold, and turns the hold off.
    The jobs service gives up on a write after 30 seconds, so never hold a write for more than 20 seconds.
  - `failNextWrites(n)` makes the next `n` writes with a valid session answer 500 and change nothing. A write without a valid session does not use up the count.
  - `setCollectionLookup(false)` makes the collection item lookup answer an empty collection. `reset()` turns it back on.
  - `applyButFailNextWrites(n)` makes each of the next `n` writes with a valid session do its work, as an accepted write does, and then answer 500. Its attempt has the outcome `failed`. A write without a valid session does not use up the count. `reset()` sets the count to 0.
  - `setSearchIncludesExpansions(true)` makes the BGG search also return the expansions whose name matches. `reset()` turns it off.
  - `setLookupCollIdOffset(n)` makes the collection item lookup by BGG id answer each item with its collection item id plus `n`, so a remove that uses the looked-up id and not the stored one can be seen. `reset()` sets 0.
  - `report().attempts` lists every write in start order with `action`, `username`, `bggId`, `collId`, `outcome` (`accepted`, `failed` or `rejected`), `startedAt` and `endedAt`.
    `endedAt` is null while the write is held. `report().maxConcurrentWrites` is the most writes in progress at one moment since the last reset.
  - `reset()` answers every held write with 503 first.
- The `cleanState` auto fixture empties the collection, resets the mock, resets the jobs service (`jobs.reset()`, which deletes every BGG job and waits until
  none runs) and resets the mock again, before every test. A spec never depends on data from another spec.

## 4. Writing specs

- Tag every spec with its IT code from the sub-issue: `test.describe('…', { tag: '@{IT_CODE}' }, …)`
  (see `coding-standards.md` §5). The baseline specs in `features/collection/smoke.spec.ts` carry
  `@smoke` because they predate the code workflow.
- Put selectors and page steps in a page object. A spec reads as the user story.
- Use web-first assertions (`await expect(locator).toBeVisible()`). Never use fixed sleeps. To wait
  for a side effect outside the page, use `expect.poll`.
- Wait for the UI before you interact: page objects call `goto()`, which waits for
  `body[data-ready="true"]`. `frontend/src/main.js` sets that attribute after it wires the UI.
- Prefer ids and roles that exist in `frontend/index.html`. If a control has none, add a stable
  id in the frontend rather than a brittle selector.
