// ── Sort field definitions ─────────────────────────────────
export const SORT_FIELDS = [
	{ field: 'name',            label: 'Name',        type: 'string',  defaultDir: 'asc'  },
	{ field: 'year',            label: 'Year',        type: 'number',  defaultDir: 'asc'  },
	{ field: 'bggRating',       label: 'Rating',      type: 'number',  defaultDir: 'desc' },
	{ field: 'minPlayers',      label: 'Min players', type: 'number',  defaultDir: 'asc'  },
	{ field: 'maxPlayers',      label: 'Max players', type: 'number',  defaultDir: 'asc'  },
	{ field: 'playTimeMinutes', label: 'Play time',   type: 'number',  defaultDir: 'asc'  },
];

export const DEFAULT_SORT = [{ field: 'name', dir: 'asc' }];

export const VIEW_MODES = ['large', 'medium', 'small', 'list', 'details'];

// ── Mutable state ──────────────────────────────────────────
export let allGames = [];
export let deleteTargetId = null;
export let bggSearchTimer = null;
export let coverPreviewTimer = null;
export let expansionSearchTimer = null;
export let bggReachable = false;   // reflects latest /api/config; false until first probe completes
export let bggConfigured = false;  // reflects latest /api/config; false until first config load
export let bggPollInterval = null; // polling handle while BGG is unavailable
export let currentViewMode = 'large';
export let sortCriteria = [...DEFAULT_SORT];

// ── State setters ──────────────────────────────────────────
// ES module exports are live bindings but cannot be reassigned from outside.
// These setters allow other modules to update shared mutable state.
export function setAllGames(val)          { allGames = val; }
export function setDeleteTargetId(val)    { deleteTargetId = val; }
export function setBggSearchTimer(val)        { bggSearchTimer = val; }
export function setCoverPreviewTimer(val)      { coverPreviewTimer = val; }
export function setExpansionSearchTimer(val)   { expansionSearchTimer = val; }
export function setBggReachable(val)      { bggReachable = val; }
export function setBggConfigured(val)     { bggConfigured = val; }
export function setBggPollInterval(val)   { bggPollInterval = val; }
export function setCurrentViewMode(val)   { currentViewMode = val; }
export function setSortCriteria(val)      { sortCriteria = val; }
