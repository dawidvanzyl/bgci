# BoardGame Collection Insights

A personal board game collection tracker. Search [BoardGameGeek](https://boardgamegeek.com) to populate game details automatically, or add games manually. Runs entirely on your local machine via Docker: the API, a PostgreSQL database and the web server.

---

## Overview

BoardGame Collection Insights lets you:

- **Search BGG** — type a game name, pick from live BGG search results, and import full details (cover art, player count, play time, rating, categories, mechanics) in one click
- **Add manually** — fill in a form yourself if you prefer not to use BGG
- **Manage your collection** — view all your games as a card grid, edit any details, or remove games
- **Filter** — search and filter your collection by name, category, or mechanic
- **Switch view modes** — choose from five display modes (large, medium, small, list, details) that persist across sessions
- **Sort your collection** — multi-level sort by name, year, rating, player count, or play time; persists across sessions
- **Sync with BGG** — BGG is the source of truth for your owned games. The app syncs your collection on startup and every 6 hours, including owned expansions. Adding or removing a game in the UI writes back to BGG automatically. A manual **↻ Sync BGG** button is available in the header when a BGG username is configured.
- **Track expansions** — each game has an Expansions tab where you can add owned expansions (from a BGG known-expansions dropdown, a BGG search, or manually). Expansion count badges appear on game cards. Expansions are hidden from the main grid and only shown within their parent game.
- **BGG availability detection** — the app continuously monitors BGG reachability. If BGG is unavailable, BGG features are automatically disabled and a status banner is shown. Your collection continues to load from the local cache. When BGG comes back online, all features are restored automatically without a page refresh.

---

## Architecture

The backend follows **Domain-Driven Design** with two bounded contexts:

```
┌─────────────────────────────┐   ┌──────────────────────────────┐
│     GameCollection BC       │   │      BggIntegration BC       │
│                             │   │                              │
│  Domain / Application /     │   │  Domain / Application /      │
│  Infrastructure             │   │  Infrastructure              │
│                             │   │                              │
│  Owns the collection of     │   │  Owns communication with BGG │
│  games and all mutations    │   │  (XML API2 reads, web writes)│
└─────────────────────────────┘   └──────────────────────────────┘
                    ▲                            │
                    │     Anti-Corruption Layer  │
                    └────────────────────────────┘
                         BGG → GameCollection
                         translation happens here
```

The jobs service is a second .NET host that runs Hangfire, runs the BGG collection writes and serves the Hangfire dashboard. The API only queues the jobs. The service keeps its jobs in its own PostgreSQL database, separate from the collection database.

### Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Vanilla JS ES modules, HTML, CSS — bundled by Vite |
| API | ASP.NET Core Web API (.NET 10) |
| CQRS | MediatR |
| Database | PostgreSQL via Dapper (stored functions) |
| Web Server | nginx (reverse proxy + static files) |
| Container | Docker / Docker Compose |

---

## Prerequisites

### To run with Docker (recommended)

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- BGG account (optional) — required only for collection sync and write-back

### To run without Docker (development)

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) — required for the Vite frontend dev server
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) — `dotnet test` needs it running, because the database tests start PostgreSQL through Testcontainers

---

## Running Locally with Docker

### Using the published image (recommended)

No source code, .NET SDK, or Node.js required — just Docker Desktop.

**1. Download the deployment files**

From the [latest GitHub Release](https://github.com/dawidvanzyl/bgci/releases/latest), download:
- `docker-compose.yml`
- `env.example` (GitHub hides the leading dot on release files)

Place both files in the same folder.

**2. Configure your environment**

```bash
cp env.example .env
```

Open `.env` and fill in your values:

```
DB_PASSWORD=choose-a-password       # required, password of the PostgreSQL database
DB_NAME=bgci                        # optional, database name
DB_USER=bgci                        # optional, database user
JOBS_DB_PASSWORD=choose-a-password  # required, password of the jobs database
JOBS_DB_NAME=bgci_jobs              # optional, jobs database name
JOBS_DB_USER=bgci_jobs              # optional, jobs database user
BGG_BEARER_TOKEN=your-token-here    # required for BGG search
BGG_USERNAME=your-bgg-username      # required for collection sync
BGG_PASSWORD=your-bgg-password      # used by the jobs service for collection write-back
```

> `DB_PASSWORD` and `JOBS_DB_PASSWORD` are required. The stack does not start without them. `DB_NAME` and `DB_USER` default to `bgci`. `JOBS_DB_NAME` and `JOBS_DB_USER` default to `bgci_jobs`.
>
> `BGG_BEARER_TOKEN` requires a formal application at [boardgamegeek.com/applications](https://boardgamegeek.com/applications), which may take several weeks to be approved. **Without it, BGG search is disabled in the UI.**
>
> `BGG_USERNAME` and `BGG_PASSWORD` are optional — without them the app works as a standalone manual collection tracker with no BGG sync.

**3. Start the application**

```bash
docker compose up -d
```

**4. Find the port and open the app**

The web UI listens on host port 15000. Set `BGCI_WEB_PORT` in `.env` to use another port. To check that the stack is up:

```bash
docker compose ps
```

Look for `0.0.0.0:15000->80/tcp` under `bgci-web`. Then open `http://localhost:15000` in your browser.

The jobs dashboard is at `http://localhost:15003`. Set `BGCI_JOBS_DASHBOARD_PORT` in `.env` to use another port. This port listens on 127.0.0.1 only. The dashboard has no login, so only this machine can open it.

**5. Stop the application**

```bash
docker compose down
```

**Data persistence**

Game data is stored in PostgreSQL, in a Docker volume named `bgci-pgdata`. The jobs service keeps its jobs in a second PostgreSQL database, in a Docker volume named `bgci-jobs-pgdata`. Your collection persists across container restarts. To wipe all data:

```bash
docker compose down -v
```

**Upgrading from 1.x**

Version 2.0.0 stores the collection in PostgreSQL instead of SQLite. This is a breaking change:

- Existing SQLite data is not migrated.
- Games from BoardGameGeek return through sync.
- Games you added manually are not carried over.
- The old `bgci-data` volume is no longer used. Remove it with `docker volume rm bgci-data`.

**Upgrading to 2.1.1**

Version 2.1.1 removes the `bgg-writer` container. The API now writes to BGG itself.

1. Keep or set `BGG_PASSWORD` in `.env`. The API now reads it.
2. Replace `docker-compose.yml` with the file from the new release.
3. Run `docker compose pull`. Every image in the compose file uses a `:latest` tag, and `docker compose up -d` does not fetch a newer `:latest` when a local copy exists. Without the pull, the old API image stays and still sends writes to the removed writer, so every BGG write fails.
4. Run `docker compose up -d --remove-orphans`. This removes the old `bgg-writer` container.
5. No data is lost. The `bgci-pgdata` volume is not changed.
6. Optional: run `docker image rm ghcr.io/dawidvanzyl/bgci-writer` to free disk space.

**Upgrading from 2.1.1**

The next version adds the jobs service and the jobs database.

1. Set `JOBS_DB_PASSWORD` in `.env`. Compare `.env` with the new `env.example` for the other new values.
2. Keep `BGG_PASSWORD` in `.env`. The jobs service now reads it, and the API no longer does. A game you add from BoardGameGeek shows as pending until the jobs service adds it to your BGG collection. A game you remove leaves your collection at once, and the jobs service removes it from your BGG collection afterwards.
2. Replace `docker-compose.yml` with the file from the new release.
3. Run `docker compose pull`. Every image in the compose file uses a `:latest` tag, and `docker compose up -d` does not fetch a newer `:latest` when a local copy exists. Without the pull, the jobs image is fetched, but the other images stay on their old versions.
4. Run `docker compose up -d`.
5. No data is lost. The `bgci-pgdata` volume is not changed. Docker creates a new `bgci-jobs-pgdata` volume for the jobs database.

---

### Running the QA stack (for developers)

Requires Docker Desktop. The `qa` profile of the root `docker-compose.yml` builds the API, the jobs service and the web server from source and replaces BoardGameGeek with a mock server (`e2e/mock-bgg`). It also runs two PostgreSQL services, one for the collection and one for the jobs service. Both keep their data in memory, so the databases start empty on every run. It is the stack the Playwright suite runs against; see `docs/coding-standards-e2e.md`.

```bash
docker compose --env-file .env.qa --profile qa up --build -d
```

Open `http://localhost:15100`. The jobs dashboard is at `http://localhost:15104` (`QA_JOBS_DASHBOARD_PORT` changes the port). To change ports, copy `.env.qa.example` to `.env.qa` and edit it. Every value has a default, so `--env-file .env.qa` is optional.

```bash
docker compose --env-file .env.qa --profile qa down -v
```

To run the production stack from the published images, copy `.env.prod.example` to `.env.prod`, set `DB_PASSWORD`, `JOBS_DB_PASSWORD` and your BGG credentials, and run:

```bash
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d
```

---

### Running the dev stack (for developers)

The `dev` profile of the root `docker-compose.yml` builds the database, the API, the jobs service, the jobs database and the web server from source and runs them against the real BoardGameGeek. Use a test BGG account.

```bash
cp .env.dev.example .env.dev
# fill in BGG_USERNAME, BGG_PASSWORD and BGG_BEARER_TOKEN in .env.dev
docker compose --env-file .env.dev --profile dev up --build -d
```

- Open `http://localhost:15200` (`DEV_WEB_PORT` changes the port).
- The jobs dashboard is at `http://localhost:15203` (`DEV_JOBS_DASHBOARD_PORT` changes the port). It listens on 127.0.0.1 only.
- To run only the database, for running the API from source, name the service: `docker compose --env-file .env.dev --profile dev up -d bgci-dev-db`. It listens on host port `DEV_DB_PORT` (default 15202), on the loopback address 127.0.0.1 only, so other machines on your network cannot reach it.
- Its data stays in the `bgci-dev-data` volume until you run `docker compose --env-file .env.dev --profile dev down -v`.
- It is separate from the QA stack, so both can run at the same time.
- Docker Compose gives variables set in your shell priority over `--env-file`. If the stack uses the wrong account, check your shell for `BGG_USERNAME`, `BGG_PASSWORD` and `BGG_BEARER_TOKEN`.

---

## Running Locally without Docker

### API

```bash
cd src/Api
dotnet run
```

The API starts on `http://localhost:5074` by default. Start the dev database first (see "Running the dev stack"; start only `bgci-dev-db`). The API also needs the jobs database: set `JobsDatabase:Host`, `JobsDatabase:Port`, `JobsDatabase:Name`, `JobsDatabase:User` and `JobsDatabase:Password` in the same file (the `JOBS_DB_*` values in `.env.dev.example`). Point the API at the collection database with `Database:Host=localhost`, `Database:Port=15202` (`DEV_DB_PORT`), `Database:Name=bgci`, `Database:User=bgci` and `Database:Password=bgci`, the values in `.env.dev.example`. Put them in the `Database` section of your local `src/Api/appsettings.Development.json`. That file is git-ignored, so each developer keeps their own copy.

The API queues BGG collection writes and does not send them. The jobs service sends them, and it reads `Bgg:Username`, `Bgg:Password` and `Bgg:BearerToken`. The API reads `Bgg:Username` and `Bgg:BearerToken` for search and sync.

### Frontend

The frontend is built with [Vite](https://vite.dev/). Run the Vite dev server alongside the API — it proxies all `/api` requests to the .NET backend automatically, so no additional configuration is needed.

```bash
cd frontend
npm install   # first time only
npm run dev
```

The app is available at `http://localhost:5173`.

> **Note:** `public/config.js` is no longer used for local development. The Vite dev server's built-in proxy handles API routing. The `config.js` mechanism is still supported if needed — create `frontend/public/config.js` (gitignored) with `window.API_OVERRIDE = 'http://localhost:5074/api'`.

---

## Configuration

| Key | Description | Default |
|---|---|---|
| `Database:Host` | Host name of the PostgreSQL server | `bgci-db` in Docker |
| `Database:Port` | Port of the PostgreSQL server | `5432` |
| `Database:Name` | Database name | `bgci` |
| `Database:User` | Database user | `bgci` |
| `Database:Password` | Database password | *(none, set `DB_PASSWORD`)* |
| `JobsDatabase:Host` | Host name of the PostgreSQL server that holds the jobs database. The API and the jobs service read this key. | `bgci-jobs-db` in Docker |
| `JobsDatabase:Port` | Port of the jobs database server. The API and the jobs service read this key. | `5432` |
| `JobsDatabase:Name` | Jobs database name. The API and the jobs service read this key. | `bgci_jobs` |
| `JobsDatabase:User` | Jobs database user. The API and the jobs service read this key. | `bgci_jobs` |
| `JobsDatabase:Password` | Jobs database password. The API and the jobs service read this key. | *(none, set `JOBS_DB_PASSWORD`)* |
| `Bgg:BearerToken` | BGG API bearer token. Without it, BGG search is disabled. Requires approval at [boardgamegeek.com/applications](https://boardgamegeek.com/applications). | *(empty)* |
| `Bgg:Username` | BGG username for collection sync. | *(empty)* |
| `Bgg:Password` | BGG password. The jobs service logs in with it to add and remove games in your BGG collection. Without it, those writes fail. | *(empty)* |

**`appsettings.json`** (production / Docker):
```json
{
  "Database": {
    "Host": "bgci-db",
    "Port": 5432,
    "Name": "bgci",
    "User": "bgci"
  }
}
```

**`appsettings.Development.json`** (local development). This file is not in the repository. Create it yourself:
```json
{
  "Database": {
    "Host": "localhost",
    "Port": 15202,
    "Name": "bgci",
    "User": "bgci",
    "Password": "bgci"
  }
}
```

**Docker environment variable override:**
```yaml
environment:
  - Database__Host=bgci-db
  - Database__Port=5432
  - Database__Name=bgci
  - Database__User=bgci
  - Database__Password=${DB_PASSWORD}
```

Note the double underscore `__` — this is the ASP.NET Core convention for nested configuration keys in environment variables.

---

## BGG Integration

Game data is fetched from the [BoardGameGeek XML API2](https://boardgamegeek.com/wiki/page/BGG_XML_API2). This is the official BGG API — no HTML scraping is involved. BGG is the source of truth for your owned game collection.

### Availability Detection

The app monitors BGG reachability at runtime using a dedicated lightweight background service:

- Probes BGG immediately on startup, before serving the first request
- Polls every **2 minutes** while BGG is unavailable (fast recovery detection)
- Polls every **15 minutes** while BGG is available (low overhead)
- When BGG becomes unavailable: BGG search is disabled, the sync button is disabled, "View on BGG" badge links are deactivated, and a status banner is shown
- When BGG is restored: all features re-enable automatically without a page refresh, and an immediate sync is triggered to catch up any changes missed while BGG was down
- The collection always loads from the local PostgreSQL database regardless of BGG availability

### Info Button & Manual Sync

BGG-sourced games display an **Info** button instead of Edit. Clicking it opens a read-only modal pre-populated with all game details and a direct link to the game's BGG page.

When `BGG_USERNAME` is configured, a **↻ Sync BGG** button appears in the header. Clicking it triggers an immediate differential sync against your BGG collection (same logic as the automatic background sync). The background sync still runs on startup and every 6 hours regardless.

### Collection Writes

BGG has no public API for writing to a collection. The BGG website writes through its own calls, and the jobs service sends the same calls. The jobs service holds the BGG password (`Bgg:Password`, or `BGG_PASSWORD` in Docker). The API holds no BGG password and never calls BGG to write. The calls are recorded in [docs/bgg-website-calls-reference.md](docs/bgg-website-calls-reference.md).

How it works:

- When you add a game from BGG, the API saves it at once with the BGG status `pending` and queues an add job. When you remove a game, the API deletes it at once and queues a remove job. The jobs service runs one job at a time, in order. A failed job retries twice. After the third failed attempt the game shows the `Failed` mark, or the remove job stays in the Failed list of the jobs dashboard, where you can requeue it.
- Every finished job writes one record to the `bgg_job_log` table of the jobs database.
- The jobs service signs in with `POST /login/api/v1`, using `Bgg:Username` and `Bgg:Password`. It keeps the session cookies in memory, so a restart needs a new login.
- It adds a game with `POST /api/collectionitem` and removes one with `DELETE /api/collectionitem/{collId}`.
- An add needs `Bgg:BearerToken`. With a blank token, the add fails before any request to BGG.
- After an add, the jobs service finds the `collId` with the BGG XML API and the bearer token. It retries while BGG answers 202.
- If BGG answers 401 or 403, the jobs service signs in again once and repeats the write.
- If BGG answers with a Cloudflare challenge page, the write fails and the error names the step.
- One write has a limit of 60 seconds.
- If a write fails, the local change stays. A failed add shows the `Failed` mark on the game.
- With no username or password, the job fails.
- One jobs service serves one account.

### Live BGG write test

A mock of BGG serves every normal test. The live test is the one check against the real BGG. It builds the write client and runs one full cycle with a test account: log in, add Action Test (BGG id 3179), find the collection item id, remove the game, and confirm that the collection no longer lists it. It needs no API process, database or frontend.

The normal test run and CI do not run it. It carries the trait `Category=LiveBgg`, CI excludes that trait, and the test skips itself unless `BGG_LIVE_TEST` is `true`. A plain `dotnet test` therefore never calls BGG.

Run it by hand with the test account. Set four environment variables, then run the filter.

```bash
export BGG_LIVE_TEST=true
export BGG_LIVE_USERNAME=<test account username>
export BGG_LIVE_PASSWORD=<test account password>
export BGG_LIVE_BEARER_TOKEN=<bearer token>
dotnet test src/BggIntegration.Infrastructure.Tests --filter "Category=LiveBgg"
```

```powershell
$env:BGG_LIVE_TEST = "true"
$env:BGG_LIVE_USERNAME = "<test account username>"
$env:BGG_LIVE_PASSWORD = "<test account password>"
$env:BGG_LIVE_BEARER_TOKEN = "<bearer token>"
dotnet test src/BggIntegration.Infrastructure.Tests --filter "Category=LiveBgg"
```

Safety rules:

- The test refuses to run, before any BGG call, when the username is not in `test-data/bgg-live-test-accounts.json`. That file must hold only the test account and never your own account.
- A missing username, password or bearer token is a failure with a message that names the variable.
- The test writes only games that `test-data/bgg-games.json` marks `liveWritable`.
- The test account must not own Action Test outside the test, because the cleanup removes it. The test removes a leftover before the add and removes the game again after the run, passed or failed.
- A manual run must not overlap a workflow run, because both use the same account.

The workflow run:

- The workflow `.github/workflows/bgg-live.yml` has no schedule. It starts only by hand, on `master` or a `milestone/*` branch. It never starts from a pull request.
- It runs in the `bgg-test` environment, which is limited to `master` and `milestone/*`. That environment holds the secrets `BGG_TEST_USERNAME`, `BGG_TEST_PASSWORD` and `BGG_BEARER_TOKEN`.
- One run at a time uses the test account. A second run waits.
- A failed test fails the run, and GitHub notifies the owner.

How to read a failure: the message reads `BGG live write failed at step '{step}': {kind}`. The step is `login`, `add`, `collection item id lookup`, `remove` or `cleanup`. The kind is one of these.

| Kind | Meaning |
|---|---|
| `Challenge` | BGG answered with a Cloudflare page. A GitHub runner has a datacenter IP. This does not mean that a write broke. |
| `Outage` | BGG answered 5xx or did not answer. |
| `LoginRejected` | BGG refused the login. |
| `WriteRefused` | BGG refused the add or the remove. |
| `LookupFailed` | BGG listed no collection item id after the add. |
| `ReadFailed` | The collection read gave an unusable answer, for example 401 for a bad bearer token. |
| `StillListed` | The collection still lists the game after the remove or the cleanup. |
| `TimeLimit` | The run or the cleanup ran out of time. |
