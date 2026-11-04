# UI Design Standard

The visual and wording rules for the bgci frontend. This document describes the existing
UI (`frontend/public/style.css` and `frontend/index.html`) and turns it into rules. A
mockup in `.design/` follows this document. Where a mockup and this document disagree, the
mockup governs that screen, and this document is updated afterwards.

> Code rules: `coding-standards-frontend.md`. Workflow rules: `coding-standards.md`.

---

## 1. Brand and Style

A dark, compact, card-based interface for a personal board game collection. The accent
colour is blue. Surfaces are layered dark greys. Corners are rounded. There is one theme.

## 2. Colours

All colours come from the CSS variables in `:root` of `style.css`. Do not add a colour
that a variable already covers.

| Token | Value | Use |
|---|---|---|
| `--bg` | `#0f1117` | Page background |
| `--surface` | `#1a1d27` | Header, cards, modals, dropdowns |
| `--surface2` | `#22263a` | Inputs, secondary buttons |
| `--border` | `#2e3250` | All borders, hover fill for secondary controls |
| `--accent` | `#4f7cff` | Primary action, focus ring |
| `--accent-h` | `#3a63e8` | Primary hover |
| `--danger` | `#e05252` | Destructive action |
| `--text` | `#e4e6f0` | Body text |
| `--text-muted` | `#8a8fa8` | Secondary text, hints |

Rules:
- Use `--accent` for exactly one primary action per view or dialog.
- Use `--danger` only for actions that remove data.
- A new token needs a reason in the story. Add it to `:root` and to this table.

## 3. Typography

- Font: `'Segoe UI', system-ui, sans-serif`. Base size 15px.
- Page title (`h1`): 1.25rem, weight 700.
- Buttons: 14px, weight 600. Small buttons (`btn-sm`): 12px.
- Form inputs: 14px. Hints use `--text-muted`.

## 4. Layout and Spacing

- Page content is centred, with a maximum width of 1400px and 24px side padding.
- The header (60px) and the toolbar and sort controls stay at the top while the grid
  scrolls.
- The collection has five view modes: large, medium, small, list and details. The choice
  persists in `localStorage` under `bgci-view-mode`. A new game view must work in all five.
- Forms use a two-column grid. A field that needs the full width uses `full`.

## 5. Shapes and Elevation

- Standard radius: `--radius` (10px). Small controls: 6px. Pills and badges: 20px.
  Modals: 14px.
- Only modals and dropdowns carry a shadow (`--shadow`). Cards use a border, not a shadow.

## 6. Components

### 6.1 Buttons

| Class | Use |
|---|---|
| `btn btn-primary` | The main action: `Save`, `+ Add Manually` |
| `btn btn-secondary` | Any other action: `Cancel`, `Sync BGG`, sort controls |
| `btn btn-danger` | Confirms a removal. Only inside a confirmation dialog. |
| `btn-icon` | Close (`×`) and other icon-only controls. Give it an `aria-label`. |
| `btn-sm` | A smaller button inside a card, row or chip area |

- A button that starts an async action shows a loading state with `setButtonLoading`,
  and text such as `Saving…` or `Removing…`. It clears with `clearButtonLoading`.
- Icon-only buttons need a `title` and an `aria-label`.

### 6.2 Modals

- Structure: `modal-overlay` > `modal` > `modal-header` (title and `×` close), then the
  body, then an action row.
- Standard width: 620px at most. Confirmation dialogs use `modal-sm` (380px).
- Clicking the `×` or `Cancel` closes a dialog without saving. Clicking the overlay does
  not close it.
- The action row puts the secondary action (`Cancel`) left of the primary or danger
  action.

### 6.3 Confirmation dialog (removal)

- Title or message names the item: `Remove "{game name}" from your collection?`
- Buttons: `Cancel` (secondary) and `Remove` (danger).
- The danger button shows `Removing…` while the request runs.

### 6.4 Forms

- Every input has a visible `label` linked with `for`. Required fields end with `*`.
- Numeric inputs set `min` and `max` where the domain has a bound.
- A field hint is a `span.hint` inside the label.

### 6.5 Dropdowns and chips

- Dropdowns (`.dropdown`) open under their trigger and close on selection or outside click.
- Sort criteria show as chips (`sort-chip`). Each chip has a direction toggle and a remove control.

### 6.6 Status and empty states

- BGG unavailable or not configured: BGG controls are disabled and a status banner
  explains why. The collection still loads.
- An empty collection shows the `empty-state` message with a hint to search BGG or add a
  game manually.

## 7. Wording

- Use sentence case for labels and messages. Use Title Case only for the app title.
- Use the user's words: "collection", "game", "expansion".
- A removal says `Remove`, never `Delete`. (The code uses `Delete` in names; the UI does
  not.)
- Verbs in buttons: `Save`, `Cancel`, `Remove`, `Add`, `Sync BGG`. A progress label ends
  with `…` and uses the `-ing` form.
- Error messages say what failed and, where possible, what the user can do. They never show
  a raw exception or a stack trace.

## 8. Checklist

Before a UI story is done:

- [ ] Colours, radius and shadow come from tokens.
- [ ] One primary action per view or dialog. Danger only for removal.
- [ ] Async buttons show a loading state and recover on failure.
- [ ] Every input has a label. Icon-only buttons have `aria-label` and `title`.
- [ ] The screen works in all five view modes, or the story says why not.
- [ ] The screen works with BGG unavailable.
- [ ] Wording follows §7.
- [ ] The result matches the cited mockup.
