# End-to-End Test Standards

Conventions for the Playwright suite in `e2e/`.

> Workflow, branching, commit, PR and comment rules: `coding-standards.md`. Those rules
> outrank this document. Selector and wording facts come from `frontend/index.html` and
> `design-standards.md`.

---

## 1. Layout

```text
test-data/
└── bgg-games.json         # The standard game data set, shared with the live suite
e2e/
├── mock-bgg/              # Mock of the BGG XML API and the bgg-writer sidecar
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
- Ports: web `QA_WEB_PORT` (default 3000), mock BGG `QA_MOCK_PORT` (default 4010).
- To run one story's tests, filter by its code: `npx playwright test --grep "@{IT_CODE}"`.
  Run the whole suite in CI, not locally.
- The suite shares one database, so it runs with one worker. Do not enable parallel runs.

## 3. Mock BGG

`mock-bgg` replaces both BoardGameGeek and the `bgg-writer` sidecar. The API reaches it
through `Bgg__BaseUrl` and `BggWriter__BaseUrl`. Tests never call the real BGG.

- It serves `search`, `thing` and `collection` as BGG XML, built from the standard game data set.
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
- It records `bgg-writer` calls. Read them with `mockBgg.writerCalls()`.
- Steer it from a spec through the `mockBgg` fixture: `setAvailable(false)`,
  `setCollection([...])`, `setWriterFailing(true)` / `setWriterFailing(false)`, `reset()`.
  `setWriterFailing(true)` makes the writer answer every add and remove with a 500. `reset()` restores
  writer success.
- The `cleanState` auto fixture empties the collection and resets the mock before every test.
  A spec never depends on data from another spec.

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
