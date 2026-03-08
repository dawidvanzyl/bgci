# BoardGame Collection Insights

A personal board game collection tracker. Search [BoardGameGeek](https://boardgamegeek.com) to populate game details automatically, or add games manually. Runs entirely on your local machine via Docker.

---

## Overview

BoardGame Collection Insights lets you:

- **Search BGG** — type a game name, pick from live BGG search results, and import full details (cover art, player count, play time, rating, categories, mechanics) in one click
- **Add manually** — fill in a form yourself if you prefer not to use BGG
- **Manage your collection** — view all your games as a card grid, edit any details, or remove games
- **Filter** — search and filter your collection by name, category, or mechanic

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
| Frontend | Vanilla JS, HTML, CSS |
| API | ASP.NET Core Web API (.NET 10) |
| CQRS | MediatR |
| Database | SQLite via Dapper |
| Web Server | nginx (reverse proxy + static files) |
| Container | Docker / Docker Compose |

---

## Prerequisites

### To run with Docker (recommended)

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### To run without Docker (development)

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A static file server or browser capable of opening local HTML files

---

## Running Locally with Docker

**Start the application:**

```bash
docker compose up --build
```

Then open [http://localhost](http://localhost) in your browser.

**Stop the application:**

```bash
docker compose down
```

**Data persistence:**

Game data is stored in a Docker volume named `bgci-data`. Your collection persists across container restarts and rebuilds. To wipe all data:

```bash
docker compose down -v
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

The frontend is plain static files — no build step required. Open `public/index.html` directly in your browser, or serve it with any static file server:

```bash
# Python (if installed)
python -m http.server 3000 --directory public

# Node.js (if installed)
npx serve public
```

The frontend automatically detects when it is running on a local dev server (any `localhost` port other than 80/443) and points API calls directly at `http://localhost:5074/api`. No configuration needed.

---

## Configuration

| Key | Description | Default |
|---|---|---|
| `Database:Path` | Path to the SQLite database file | `/data/bgci.db` |

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

Game data is fetched from the [BoardGameGeek XML API2](https://boardgamegeek.com/wiki/page/BGG_XML_API2). This is the official BGG API — no HTML scraping is involved.
