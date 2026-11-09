# BGG XML API2 reference snapshot

Source: https://boardgamegeek.com/wiki/page/BGG_XML_API2
Retrieved: 2026-11-05
Status of the source: the page calls XMLAPI2 a beta. It can change. The page blocks automated fetches (HTTP 403), so read it in a browser.

This file is a summary in our own words. It is the fixed input for planning the Bruno collection (epic #73).

## General rules

- Root path: `https://boardgamegeek.com/xmlapi2/`. Do not use the `www` host. It can break request authorization.
- Rate limit: too many requests give HTTP 500 or 503. A wait of 5 seconds between requests currently works.
- The page does not describe tokens or API keys. It says only that `collection` with `showprivate=1` needs the login cookies of the owner.
- The page links to the XML API terms of use and a usage guide. Read both before heavy use.
- CSV dump: `https://boardgamegeek.com/data_dumps/bg_ranks`. It holds the name, id, rank and average rating of every game. The page calls it the preferred way to get all names and ranks. It is part of the XML API for licensing.

## Endpoints

Paths are relative to the root path.

| Endpoint | Parameters |
|---|---|
| `thing` | `id` (comma list, max 20), `type`, `versions`, `videos`, `stats`, `marketplace`, `comments`, `ratingcomments`, `page`, `pagesize` (10-100). Not supported: `historical`, `from`, `to`. |
| `family` | `id` (comma list), `type` (`rpg`, `rpgperiodical`, `boardgamefamily`) |
| `forumlist` | `id`, `type` (`thing` or `family`) |
| `forum` | `id`, `page` (page size 50) |
| `thread` | `id`, `minarticleid`, `minarticledate` (date, or date and time), `count` (max 1000). Not supported: `username`. |
| `user` | `name` (one user), `buddies`, `guilds`, `hot`, `top`, `domain` (`boardgame`, `rpg`, `videogame`), `page` |
| `guild` | `id`, `members`, `sort` (`username` or `date`), `page` (page size 25) |
| `plays` | `username`, `id`, `type` (`thing` or `family`), `mindate`, `maxdate`, `subtype`, `page` (page size 100). Needs `username`, or `id` with `type`. |
| `collection` | See the next section. |
| `hot` | `type` (`boardgame`, `rpg`, `videogame`, `boardgameperson`, `rpgperson`, `boardgamecompany`, `rpgcompany`, `videogamecompany`) |
| `search` | `query`, `type`, `exact` |
| `geeklist` | The page says it is not yet updated to XMLAPI2. |

Allowed `thing` types: `boardgame`, `boardgameexpansion`, `boardgameaccessory`, `videogame`, `rpgitem`, `rpgissue`.

Allowed `plays` subtypes: `boardgame` (default), `boardgameexpansion`, `boardgameaccessory`, `boardgameintegration`, `boardgamecompilation`, `boardgameimplementation`, `rpg`, `rpgitem`, `videogame`.

Allowed `search` types: `rpgitem`, `videogame`, `boardgame`, `boardgameaccessory`, `boardgameexpansion`, `boardgamedesigner`. A comma list is allowed.

Notes on `thing`:

- `comments` and `ratingcomments` both fill the `<comments>` node. If you send both, `comments` wins.
- `ratingcomments` results are sorted by rating, lowest first.

## Collection

Parameters:

- Identity and shape: `username`, `version`, `subtype`, `excludesubtype`, `id` (comma list), `brief`, `stats`, `collid`, `showprivate`, `modifiedsince` (date, or date and time).
- Status filters, each 0 or 1: `own`, `rated`, `played`, `comment`, `trade`, `want`, `wishlist`, `preordered`, `wanttoplay`, `wanttobuy`, `prevowned`, `hasparts`, `wantparts`.
- Value filters: `wishlistpriority` (1-5), `minrating` (1-10), `rating` (1-10, this is the maximum), `minbggrating` (1-10, 0 is ignored, -1 finds items with no BGG rating), `bggrating` (1-10, this is the maximum), `minplays`, `maxplays`.

Behaviour to remember:

- HTTP 202 means BGG queued the request. Retry with a delay until the status is not 202.
- The default `subtype=boardgame` returns games and expansions. It labels the expansions as `boardgame`. To separate them, call with `excludesubtype=boardgameexpansion`. Then call again with `subtype=boardgameexpansion`.
- The names `rating` and `bggrating` are maximum filters, although they do not say so. `maxplays` is a true maximum.
- `user` pages are documented as 100 records. The page says the real size looks like 1000.
