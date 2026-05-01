# BoardGame Collection Insights

A personal board game collection tracker. Search [BoardGameGeek](https://boardgamegeek.com) to populate game details automatically, or add games manually. Runs entirely on your local machine via Docker as two containers: the main API and a `bgg-writer` sidecar.

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
- **Track extensions** — each game has an Extensions tab where you can add owned expansions (from a BGG known-expansions dropdown, a BGG search, or manually). Extension count badges appear on game cards. Expansions are hidden from the main grid and only shown within their parent game.
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
│  Owns the collection of     │   │  Owns communication with     │
│  games and all mutations    │   │  the BGG XML API2            │
└─────────────────────────────┘   └──────────────────────────────┘
                    ▲                            │
                    │     Anti-Corruption Layer  │
                    └────────────────────────────┘
                         BGG → GameCollection
                         translation happens here
```

### Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Vanilla JS ES modules, HTML, CSS — bundled by Vite |
| API | ASP.NET Core Web API (.NET 10) |
| BGG Write Layer | Node.js + Playwright (Chromium + stealth) |
| CQRS | MediatR |
| Database | SQLite via Dapper |
| Web Server | nginx (reverse proxy + static files) |
| Container | Docker / Docker Compose |

---

## Prerequisites

### To run with Docker (recommended)

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- BGG account (optional) — required only for collection sync and write-back

### To run without Docker (development)

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) — required for both the Vite frontend dev server and `bgg-writer`

---

## Running Locally with Docker

### Using the published image (recommended)

No source code, .NET SDK, or Node.js required — just Docker Desktop.

**1. Download the deployment files**

From the [latest GitHub Release](https://github.com/dawidvanzyl/bgci/releases/latest), download:
- `docker-compose.yml`
- `.env.example`

Place both files in the same folder.

**2. Configure your environment**

```bash
cp .env.example .env
```

Open `.env` and fill in your values:

```
BGG_BEARER_TOKEN=your-token-here    # required for BGG search
BGG_USERNAME=your-bgg-username      # required for collection sync
BGG_PASSWORD=your-bgg-password      # required for collection write-back
```

> `BGG_BEARER_TOKEN` requires a formal application at [boardgamegeek.com/applications](https://boardgamegeek.com/applications), which may take several weeks to be approved. **Without it, BGG search is disabled in the UI.**
>
> `BGG_USERNAME` and `BGG_PASSWORD` are optional — without them the app works as a standalone manual collection tracker with no BGG sync.
>
> `BGG_WRITER_BASE_URL` is pre-configured in `docker-compose.yml` — no manual entry needed.

**3. Start the application**

```bash
docker compose up -d
```

**4. Find the port and open the app**

The web UI is assigned a random available port on your machine. To find it:

```bash
docker compose ps
```

Look for the port mapping under `bgci-web`, for example `0.0.0.0:49152->80/tcp`. Then open `http://localhost:49152` in your browser (substitute your actual port).

**5. Stop the application**

```bash
docker compose down
```

**Data persistence**

Game data is stored in a Docker volume named `bgci-data`. Your collection persists across container restarts. To wipe all data:

```bash
docker compose down -v
```

---

### Building from source (for developers)

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker Desktop. Node.js is not required locally — Docker builds both the `bgg-writer` and frontend images automatically.

**Start the application:**

```bash
docker compose up --build
```

Find the assigned port with `docker compose ps`, then open `http://localhost:<port>` in your browser.

**Stop the application:**

```bash
docker compose down
```

---

## Running Locally without Docker

### API

```bash
cd src/Api
dotnet run
```

The API starts on `http://localhost:5074` by default. In development mode it uses `appsettings.Development.json`, which stores the SQLite database as `bgci.db` in the `src/Api/` working directory.

### Frontend

The frontend is built with [Vite](https://vite.dev/). Run the Vite dev server alongside the API — it proxies all `/api` requests to the .NET backend automatically, so no additional configuration is needed.

```bash
cd frontend
npm install   # first time only
npm run dev
```

The app is available at `http://localhost:5173`.

> **Note:** `public/config.js` is no longer used for local development. The Vite dev server's built-in proxy handles API routing. The `config.js` mechanism is still supported if needed — create `frontend/public/config.js` (gitignored) with `window.API_OVERRIDE = 'http://localhost:5074/api'`.

### bgg-writer

```bash
cd node/bgg-writer
cp .env.example .env
# fill in BGG_PASSWORD in .env
npm install
npx playwright install chromium
node src/index.js
```

The .NET API's `appsettings.Development.json` already points `BggWriter:BaseUrl` at `http://localhost:3001` — no additional configuration needed.

To debug with the Playwright Inspector (visible Chromium browser with step-through execution):

```bash
PWDEBUG=1 node src/index.js
```

To attach a Node.js debugger (VS Code or similar):

```bash
node --inspect src/index.js
```

Then attach via the VS Code "Node.js: Attach" launch configuration on `localhost:9229`.

---

## Configuration

| Key | Description | Default |
|---|---|---|
| `Database:Path` | Path to the SQLite database file | `/data/bgci.db` |
| `Bgg:BearerToken` | BGG API bearer token. Without it, BGG search is disabled. Requires approval at [boardgamegeek.com/applications](https://boardgamegeek.com/applications). | *(empty)* |
| `Bgg:Username` | BGG username for collection sync. | *(empty)* |
| `BggWriter:BaseUrl` | Internal URL of the `bgg-writer` sidecar. Pre-configured in `docker-compose.yml`. | *(empty)* |

**`appsettings.json`** (production / Docker):
```json
{
  "Database": {
    "Path": "/data/bgci.db"
  }
}
```

**`appsettings.Development.json`** (local development):
```json
{
  "Database": {
    "Path": "bgci.db"
  }
}
```

**Docker environment variable override:**
```yaml
environment:
  - Database__Path=/data/bgci.db
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
- The collection always loads from the local SQLite cache regardless of BGG availability

### Info Button & Manual Sync

BGG-sourced games display an **Info** button instead of Edit. Clicking it opens a read-only modal pre-populated with all game details and a direct link to the game's BGG page.

When `BGG_USERNAME` is configured, a **↻ Sync BGG** button appears in the header. Clicking it triggers an immediate differential sync against your BGG collection (same logic as the automatic background sync). The background sync still runs on startup and every 6 hours regardless.

### Collection Writes — bgg-writer

**Why it exists**

BGG's collection write endpoint (`geekcollection.php`) is protected by Cloudflare bot detection. Any plain HTTP client — regardless of headers or cookies — receives a 403 response with a JavaScript challenge page. This cannot be bypassed without a real browser that executes JavaScript.

**How it works**

`bgg-writer` launches a headless Chromium browser via [Playwright](https://playwright.dev/) with the stealth plugin active (masking the headless fingerprint). It navigates to the BGG login page, fills in the credentials, and submits the form. Once authenticated, it calls `geekcollection.php` via `page.evaluate(fetch(...))` from within the browser context — Cloudflare sees a legitimate browser with valid JavaScript execution and session cookies, and allows the request through.

**Architecture**

`bgg-writer` runs as a separate Docker container alongside the main API. It is only reachable by `bgci-api` on the internal Docker network — no port is exposed externally. It holds `BGG_PASSWORD` directly; the main .NET API never sees it.
