# End-to-End Test Standards

Conventions for the Playwright suite in `e2e/`.

> Workflow, branching, commit, PR and comment rules: `coding-standards.md`. Those rules
> outrank this document. Selector and wording facts come from `frontend/index.html` and
> `design-standards.md`.

---

## 1. Layout

```text
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

- The stack is the `qa` profile of the root `docker-compose.yml`. It has no volumes, so every `up`
  starts with an empty SQLite database. Production is `docker-compose.prod.yml`.
- Ports: web `QA_WEB_PORT` (default 3000), mock BGG `QA_MOCK_PORT` (default 4010).
- To run one story's tests, filter by its code: `npx playwright test --grep "@{IT_CODE}"`.
  Run the whole suite in CI, not locally.
- The suite shares one database, so it runs with one worker. Do not enable parallel runs.

## 3. Mock BGG

`mock-bgg` replaces both BoardGameGeek and the `bgg-writer` sidecar. The API reaches it
through `Bgg__BaseUrl` and `BggWriter__BaseUrl`. Tests never call the real BGG.

- It serves `search`, `thing` and `collection` as BGG XML, built from `mock-bgg/games.mjs`.
  Add a game there when a spec needs one. Use real BGG ids so the data reads naturally.
- It records `bgg-writer` calls. Read them with `mockBgg.writerCalls()`.
- Steer it from a spec through the `mockBgg` fixture: `setAvailable(false)`,
  `setCollection([...])`, `reset()`.
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
