const API = window.API_OVERRIDE ?? '/api';

// ── Sort field definitions ─────────────────────────────────
const SORT_FIELDS = [
	{ field: 'name',            label: 'Name',        type: 'string',  defaultDir: 'asc'  },
	{ field: 'year',            label: 'Year',        type: 'number',  defaultDir: 'asc'  },
	{ field: 'bggRating',       label: 'Rating',      type: 'number',  defaultDir: 'desc' },
	{ field: 'minPlayers',      label: 'Min players', type: 'number',  defaultDir: 'asc'  },
	{ field: 'maxPlayers',      label: 'Max players', type: 'number',  defaultDir: 'asc'  },
	{ field: 'playTimeMinutes', label: 'Play time',   type: 'number',  defaultDir: 'asc'  },
];
const DEFAULT_SORT = [{ field: 'name', dir: 'asc' }];
const VIEW_MODES = ['large', 'medium', 'small', 'list', 'details'];

// ── State ──────────────────────────────────────────────────
let allGames = [];
let deleteTargetId = null;
let bggSearchTimer = null;
let coverPreviewTimer = null;
let bggReachable = false;   // reflects latest /api/config; false until first probe completes
let bggConfigured = false;  // reflects latest /api/config; false until first config load
let bggPollInterval = null; // polling handle while BGG is unavailable
let currentViewMode = 'large';
let sortCriteria = [...DEFAULT_SORT];

// ── Loading / error helpers ────────────────────────────────
const SPINNER_SVG = '<svg class="btn-spinner" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round"><path d="M12 2a10 10 0 0 1 10 10" /></svg>';

function setButtonLoading(btn, label) {
	btn.disabled = true;
	btn.dataset.originalText = btn.textContent;
	btn.innerHTML = `${SPINNER_SVG}${label}`;
}

function clearButtonLoading(btn) {
	btn.disabled = false;
	btn.textContent = btn.dataset.originalText;
}

function showInlineError(anchorEl, message) {
	const existing = anchorEl.parentElement.querySelector('.form-error');
	if (existing) existing.remove();
	const el = document.createElement('p');
	el.className = 'form-error';
	el.textContent = message;
	anchorEl.parentElement.insertBefore(el, anchorEl);
	setTimeout(() => el.remove(), 5000);
}

function showSyncError(message) {
	const syncBtn = document.getElementById('btn-sync-bgg');
	const existing = syncBtn.parentElement.querySelector('.sync-error');
	if (existing) existing.remove();
	const el = document.createElement('span');
	el.className = 'sync-error';
	el.textContent = message;
	el.style.cssText = 'color:var(--danger);font-size:13px;';
	syncBtn.parentElement.insertBefore(el, syncBtn);
	setTimeout(() => el.remove(), 5000);
}

// ── DOM refs ───────────────────────────────────────────────
const gameGrid       = document.getElementById('game-grid');
const emptyState     = document.getElementById('empty-state');
const filterInput    = document.getElementById('filter-input');
const bggSearchInput = document.getElementById('bgg-search-input');
const bggResults     = document.getElementById('bgg-search-results');
const modalOverlay   = document.getElementById('modal-overlay');
const modalTitle     = document.getElementById('modal-title');
const gameForm       = document.getElementById('game-form');
const confirmOverlay = document.getElementById('confirm-overlay');
const confirmMessage = document.getElementById('confirm-message');

// ── Bootstrap ──────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', async () => {
	// Restore view mode before first render
	const savedMode = localStorage.getItem('bgci-view-mode') || 'large';
	applyViewMode(savedMode);

	// Restore sort criteria
	try {
		const saved = localStorage.getItem('bgci-sort');
		if (saved) {
			const parsed = JSON.parse(saved);
			// Sanitize: keep only known fields with valid dir values
			const sanitized = parsed.filter(c =>
				SORT_FIELDS.some(f => f.field === c.field) && (c.dir === 'asc' || c.dir === 'desc')
			);
			sortCriteria = sanitized.length > 0 ? sanitized : [...DEFAULT_SORT];
		}
	} catch { sortCriteria = [...DEFAULT_SORT]; }
	renderSortBar();

	const config = await loadConfig();
	if (config.version) {
		const badge = document.getElementById('app-version');
		if (badge) badge.textContent = (config.version === 'dev' || config.version === 'latest') ? config.version : `v${config.version}`;
	}
	applyBggAvailability(config);
	if (config.bggSyncEnabled) {
		document.getElementById('btn-sync-bgg').style.display = '';
	}
	loadGames();
	bindEvents(config);
});

async function loadConfig() {
	try {
		const res = await fetch(`${API}/config`);
		return await res.json();
	} catch {
		return { bggConfigured: false, bggReachable: false, bggSearchEnabled: false, bggSyncEnabled: false };
	}
}

// ── BGG Availability ───────────────────────────────────────

// Single entry point for all BGG-related UI state.
// Called on initial load and on each polling check.
function applyBggAvailability(config) {
	const wasReachable = bggReachable;
	bggReachable = config.bggReachable ?? false;
	bggConfigured = config.bggConfigured ?? false;

	initBggSearch(config);
	applyBggBanner(bggReachable);

	// Enable/disable the sync button based on current reachability
	const syncBtn = document.getElementById('btn-sync-bgg');
	if (syncBtn) syncBtn.disabled = !bggReachable;

	// Re-render cards so BGG badge links reflect current reachability
	if (wasReachable !== bggReachable) {
		renderGames(filterInput.value.trim().toLowerCase());
	}

	// Start or stop polling
	if (!bggReachable && !bggPollInterval) {
		bggPollInterval = setInterval(checkBggStatus, 2 * 60 * 1000);
	} else if (bggReachable && bggPollInterval) {
		clearInterval(bggPollInterval);
		bggPollInterval = null;
	}
}

async function checkBggStatus() {
	const config = await loadConfig();
	applyBggAvailability(config);
}

function applyBggBanner(reachable) {
	const existingBanner = document.getElementById('bgg-status-banner');
	if (!reachable) {
		if (!existingBanner) {
			const banner = document.createElement('div');
			banner.id = 'bgg-status-banner';
			banner.className = 'bgg-status-banner';
			banner.textContent = 'BGG is currently unavailable \u2014 your collection is loaded from cache';
			// Insert before the main toolbar, inside <main>
			const main = document.querySelector('main');
			main.insertBefore(banner, main.firstChild);
		}
	} else {
		if (existingBanner) existingBanner.remove();
	}
}

// Distinguishes between BGG unconfigured (permanent, admin concern) and BGG unavailable (transient).
// Called on every applyBggAvailability() — idempotent: clears previous state before re-applying.
function initBggSearch(config) {
	const wrap = bggSearchInput.closest('.bgg-search-wrap');

	// Clear previous notice so we can re-evaluate cleanly on restore
	const existingNotice = wrap.querySelector('.bgg-search-notice');
	if (existingNotice) existingNotice.remove();
	bggSearchInput.classList.remove('bgg-search-disabled');
	bggSearchInput.removeAttribute('disabled');

	if (config.bggSearchEnabled) {
		// BGG is configured and reachable — fully enabled
		bggSearchInput.placeholder = 'Search BGG to add a game...';
	} else if (!config.bggReachable && bggConfigured) {
		// BGG is configured but currently unreachable — transient unavailable state
		bggSearchInput.disabled = true;
		bggSearchInput.placeholder = 'BGG unavailable';
		bggSearchInput.classList.add('bgg-search-disabled');
	} else {
		// BGG is not configured — permanent state, show setup notice
		bggSearchInput.disabled = true;
		bggSearchInput.placeholder = 'BGG search unavailable \u2014 no API token configured';
		bggSearchInput.classList.add('bgg-search-disabled');
		const notice = document.createElement('p');
		notice.className = 'bgg-search-notice';
		notice.innerHTML = 'Requires a BGG API token. See <a href="https://boardgamegeek.com/applications" target="_blank" rel="noopener">boardgamegeek.com/applications</a>.';
		wrap.appendChild(notice);
	}
}

function bindEvents(config) {
	document.getElementById('btn-add-manual').addEventListener('click', () => openModal());
	document.getElementById('modal-close').addEventListener('click', closeModal);
	document.getElementById('btn-cancel').addEventListener('click', closeModal);
	gameForm.addEventListener('submit', handleFormSubmit);

	filterInput.addEventListener('input', () => renderGames(filterInput.value.trim().toLowerCase()));

	// BGG search input listener — always bound; the input itself is disabled when unavailable/unconfigured
	bggSearchInput.addEventListener('input', () => {
		if (!bggReachable || !bggConfigured) return;
		clearTimeout(bggSearchTimer);
		const q = bggSearchInput.value.trim();
		if (q.length < 2) { hideBggResults(); return; }
		bggSearchTimer = setTimeout(() => searchBgg(q), 400);
	});

	document.addEventListener('click', (e) => {
		if (!bggResults.contains(e.target) && e.target !== bggSearchInput) hideBggResults();
	});

	document.getElementById('form-cover').addEventListener('input', e => {
		clearTimeout(coverPreviewTimer);
		const url = e.target.value.trim();
		coverPreviewTimer = setTimeout(() => updateCoverPreview(url), 400);
	});

	document.getElementById('btn-confirm-cancel').addEventListener('click', () => {
		confirmOverlay.classList.add('hidden');
		deleteTargetId = null;
	});

	document.getElementById('btn-confirm-delete').addEventListener('click', async () => {
		if (!deleteTargetId) return;
		const confirmBtn = document.getElementById('btn-confirm-delete');
		setButtonLoading(confirmBtn, 'Removing\u2026');
		try {
			await deleteGame(deleteTargetId);
			confirmOverlay.classList.add('hidden');
			deleteTargetId = null;
		} catch (err) {
			showInlineError(confirmBtn, err.message);
		} finally {
			clearButtonLoading(confirmBtn);
		}
	});

	// View toggle
	document.getElementById('view-toggle').addEventListener('click', e => {
		const btn = e.target.closest('.view-toggle-btn');
		if (!btn) return;
		applyViewMode(btn.dataset.view);
		renderGames(filterInput.value.trim().toLowerCase());
	});

	// Sync BGG button
	const syncBtn = document.getElementById('btn-sync-bgg');
	syncBtn.addEventListener('click', async () => {
		setButtonLoading(syncBtn, 'Syncing\u2026');
		try {
			const res = await fetch(`${API}/sync/bgg`, { method: 'POST' });
			if (!res.ok) throw new Error(await res.text());
			await loadGames();
		} catch (err) {
			showSyncError(err.message || 'Sync failed.');
		} finally {
			clearButtonLoading(syncBtn);
		}
	});

	// Sort bar — add button
	document.getElementById('btn-sort-add').addEventListener('click', e => {
		e.stopPropagation();
		toggleSortDropdown();
	});

	// Sort bar — clear button
	document.getElementById('btn-sort-clear').addEventListener('click', () => {
		sortCriteria = [...DEFAULT_SORT];
		saveSort();
		renderSortBar();
		renderGames(filterInput.value.trim().toLowerCase());
	});

	// Sort bar — chip interactions (delegated)
	document.getElementById('sort-chips').addEventListener('click', e => {
		const dirBtn = e.target.closest('.sort-chip-dir');
		const removeBtn = e.target.closest('.sort-chip-remove');
		if (dirBtn) {
			const field = dirBtn.closest('.sort-chip').dataset.field;
			const c = sortCriteria.find(x => x.field === field);
			if (c) { c.dir = c.dir === 'asc' ? 'desc' : 'asc'; saveSort(); renderSortBar(); renderGames(filterInput.value.trim().toLowerCase()); }
		} else if (removeBtn) {
			const field = removeBtn.closest('.sort-chip').dataset.field;
			sortCriteria = sortCriteria.filter(x => x.field !== field);
			if (sortCriteria.length === 0) sortCriteria = [...DEFAULT_SORT];
			saveSort(); renderSortBar(); renderGames(filterInput.value.trim().toLowerCase());
		}
	});

	// Close sort dropdown on outside click
	document.addEventListener('click', () => hideSortDropdown());
}

// ── Sort ───────────────────────────────────────────────────
function saveSort() {
	localStorage.setItem('bgci-sort', JSON.stringify(sortCriteria));
}

function sortGames(games) {
	return [...games].sort((a, b) => {
		for (const { field, dir } of sortCriteria) {
			const def = SORT_FIELDS.find(f => f.field === field);
			if (!def) continue;  // skip unknown/stale fields
			const av = a[field] ?? null;
			const bv = b[field] ?? null;
			// nulls always last
			if (av === null && bv === null) continue;
			if (av === null) return 1;
			if (bv === null) return -1;
			let cmp = 0;
			if (def.type === 'string') {
				cmp = av.localeCompare(bv);
			} else {
				cmp = av - bv;
			}
			if (cmp !== 0) return dir === 'asc' ? cmp : -cmp;
		}
		return 0;
	});
}

function renderSortBar() {
	const chipsEl  = document.getElementById('sort-chips');
	const clearBtn = document.getElementById('btn-sort-clear');
	const addBtn   = document.getElementById('btn-sort-add');

	chipsEl.innerHTML = '';
	sortCriteria.forEach(({ field, dir }) => {
		const def = SORT_FIELDS.find(f => f.field === field);
		if (!def) return;
		const chip = document.createElement('div');
		chip.className = 'sort-chip';
		chip.dataset.field = field;
		chip.innerHTML = `
			<span class="sort-chip-label">${def.label}</span>
			<button class="sort-chip-dir" title="Toggle direction" aria-label="Toggle sort direction for ${def.label}">${dir === 'asc' ? '&#8593;' : '&#8595;'}</button>
			<button class="sort-chip-remove" title="Remove" aria-label="Remove sort for ${def.label}">&times;</button>
		`;
		chipsEl.appendChild(chip);
	});

	// Show Clear only when not at default
	const isDefault = sortCriteria.length === 1 && sortCriteria[0].field === 'name' && sortCriteria[0].dir === 'asc';
	clearBtn.hidden = isDefault;

	// Hide Add when all fields are used
	addBtn.hidden = sortCriteria.length >= SORT_FIELDS.length;
}

function toggleSortDropdown() {
	const dropdown = document.getElementById('sort-add-dropdown');
	if (!dropdown.hidden) { hideSortDropdown(); return; }

	const used = new Set(sortCriteria.map(x => x.field));
	dropdown.innerHTML = '';
	SORT_FIELDS.filter(f => !used.has(f.field)).forEach(f => {
		const li = document.createElement('li');
		li.textContent = f.label;
		li.addEventListener('click', e => {
			e.stopPropagation();
			sortCriteria.push({ field: f.field, dir: f.defaultDir });
			saveSort();
			hideSortDropdown();
			renderSortBar();
			renderGames(filterInput.value.trim().toLowerCase());
		});
		dropdown.appendChild(li);
	});
	dropdown.hidden = false;
}

function hideSortDropdown() {
	document.getElementById('sort-add-dropdown').hidden = true;
}

// ── View Mode ──────────────────────────────────────────────
function applyViewMode(mode) {
	const nextMode = VIEW_MODES.includes(mode) ? mode : 'large';
	currentViewMode = nextMode;
	localStorage.setItem('bgci-view-mode', nextMode);

	// Update grid modifier class
	VIEW_MODES.forEach(m => gameGrid.classList.remove(`game-grid--${m}`));
	gameGrid.classList.add(`game-grid--${nextMode}`);

	// Update active toggle button
	document.querySelectorAll('.view-toggle-btn').forEach(btn => {
		btn.classList.toggle('active', btn.dataset.view === nextMode);
	});
}

// ── API calls ──────────────────────────────────────────────
async function loadGames() {
	const res = await fetch(`${API}/games`);
	allGames = await res.json();
	renderGames(filterInput.value.trim().toLowerCase());
}

async function saveGame(payload, id) {
	const url    = id ? `${API}/games/${id}` : `${API}/games`;
	const method = id ? 'PUT' : 'POST';
	const res = await fetch(url, {
		method,
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify(payload)
	});
	if (!res.ok) throw new Error(await res.text());
}

async function saveGameFromBgg(payload) {
	const res = await fetch(`${API}/games/from-bgg`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify(payload)
	});
	if (!res.ok) throw new Error(await res.text());
}

async function deleteGame(id) {
	const res = await fetch(`${API}/games/${id}`, { method: 'DELETE' });
	if (!res.ok) throw new Error(await res.text());
	await loadGames();
}

async function searchBgg(q) {
	showBggLoading();
	try {
		const res = await fetch(`${API}/bgg/search?query=${encodeURIComponent(q)}`);
		const data = await res.json();
		renderBggResults(data);
	} catch {
		hideBggResults();
	}
}

async function fetchBggPreview(bggId) {
	const res = await fetch(`${API}/bgg/game/${bggId}/preview`);
	if (!res.ok) return null;
	return res.json();
}

// ── Render ─────────────────────────────────────────────────
function renderGames(filter = '') {
	const filtered = filter
		? allGames.filter(g =>
			g.name.toLowerCase().includes(filter) ||
			(g.categories || []).some(c => c.toLowerCase().includes(filter)) ||
			(g.mechanics  || []).some(m => m.toLowerCase().includes(filter))
		)
		: allGames;

	const sorted = sortGames(filtered);

	// Remove existing cards but keep the empty state
	[...gameGrid.querySelectorAll('.game-card')].forEach(el => el.remove());

	if (sorted.length === 0) {
		emptyState.style.display = '';
		return;
	}
	emptyState.style.display = 'none';

	sorted.forEach(game => {
		gameGrid.appendChild(buildCard(game));
	});
}

function buildCard(game) {
	const card = document.createElement('div');
	card.dataset.id = game.id;

	const ratingBadge = game.bggRating
		? `<span class="badge badge-rating">&#9733; ${Number(game.bggRating).toFixed(1)}</span>`
		: '';

	const playersBadge = game.minPlayers
		? `<span class="badge badge-players">&#128100; ${game.minPlayers === game.maxPlayers ? game.minPlayers : `${game.minPlayers}\u2013${game.maxPlayers}`}</span>`
		: '';

	const timeBadge = game.playTimeMinutes
		? `<span class="badge">&#9201; ${game.playTimeMinutes} min</span>`
		: '';

	const bggBadge = game.isBggSourced
		? bggReachable
			? `<a class="bgg-badge" href="https://boardgamegeek.com/boardgame/${game.bggId}" target="_blank" rel="noopener" title="View on BoardGameGeek"><img src="/bgg-logo.svg" alt="BGG" class="bgg-badge-logo" /></a>`
			: `<span class="bgg-badge bgg-badge--disabled" title="BGG unavailable"><img src="/bgg-logo.svg" alt="BGG" class="bgg-badge-logo" /></span>`
		: '';

	const infoOrEdit = game.isBggSourced
		? `<button class="btn btn-secondary btn-info">Info</button>`
		: `<button class="btn btn-secondary btn-edit">&#9998; Edit</button>`;

	const deleteBtn = `<button class="btn btn-danger btn-delete">Remove</button>`;

	if (currentViewMode === 'list') {
		card.className = 'game-card game-card--list';
		card.innerHTML = `
			<div class="game-card-body">
				<div class="game-card-title">${esc(game.name)}${game.year ? ` <span class="game-card-year">(${game.year})</span>` : ''}</div>
				<div class="game-card-meta">${bggBadge}${ratingBadge}${playersBadge}${timeBadge}</div>
			</div>
			<div class="game-card-actions">
				${infoOrEdit}
				${deleteBtn}
			</div>
		`;
	} else if (currentViewMode === 'details') {
		const thumbHtml = game.coverImageUrl
			? `<img class="game-card-thumb" src="${esc(game.coverImageUrl)}" alt="${esc(game.name)}" loading="lazy" />`
			: `<div class="game-card-thumb-placeholder">&#127921;</div>`;

		const allTags = [...(game.categories || []), ...(game.mechanics || [])]
			.map(t => `<span class="tag">${esc(t)}</span>`).join('');

		card.className = 'game-card game-card--details';
		card.innerHTML = `
			${thumbHtml}
			<div class="game-card-body">
				<div class="game-card-title">${esc(game.name)}</div>
				${game.year ? `<div class="game-card-year">${game.year}</div>` : ''}
				<div class="game-card-meta">${bggBadge}${ratingBadge}${playersBadge}${timeBadge}</div>
				${game.description ? `<div class="game-description">${esc(game.description)}</div>` : ''}
				${allTags ? `<div class="game-card-categories">${allTags}</div>` : ''}
			</div>
			<div class="game-card-actions">
				${infoOrEdit}
				${deleteBtn}
			</div>
		`;
	} else if (currentViewMode === 'small') {
		const coverHtml = game.coverImageUrl
			? `<img class="game-card-cover" src="${esc(game.coverImageUrl)}" alt="${esc(game.name)}" loading="lazy" />`
			: `<div class="game-card-cover-placeholder">&#127921;</div>`;

		const infoOrEditSmall = game.isBggSourced
			? `<button class="btn btn-secondary btn-info btn-icon-only" title="Info">&#8505;</button>`
			: `<button class="btn btn-secondary btn-edit btn-icon-only" title="Edit">&#9998;</button>`;
		const deleteBtnSmall = `<button class="btn btn-danger btn-delete btn-icon-only" title="Remove">&#10005;</button>`;

		const ratingBadgeSmall = game.bggRating
			? `<span class="badge badge-rating badge-compact">${Number(game.bggRating).toFixed(1)}</span>`
			: '';
		const playersBadgeSmall = game.minPlayers
			? `<span class="badge badge-players badge-compact">${game.minPlayers === game.maxPlayers ? game.minPlayers : `${game.minPlayers}\u2013${game.maxPlayers}`}</span>`
			: '';

		card.className = 'game-card game-card--small';
		card.innerHTML = `
			${coverHtml}
			<div class="game-card-body">
				<div class="game-card-title">${esc(game.name)}</div>
				<div class="game-card-meta">${bggBadge}${ratingBadgeSmall}${playersBadgeSmall}</div>
			</div>
			<div class="game-card-actions">
				${infoOrEditSmall}
				${deleteBtnSmall}
			</div>
		`;
	} else if (currentViewMode === 'medium') {
		const coverHtml = game.coverImageUrl
			? `<img class="game-card-cover" src="${esc(game.coverImageUrl)}" alt="${esc(game.name)}" loading="lazy" />`
			: `<div class="game-card-cover-placeholder">&#127921;</div>`;

		card.className = 'game-card game-card--medium';
		card.innerHTML = `
			${coverHtml}
			<div class="game-card-body">
				<div class="game-card-title">${esc(game.name)}</div>
				${game.year ? `<div class="game-card-year">${game.year}</div>` : ''}
				<div class="game-card-meta">${bggBadge}${ratingBadge}${playersBadge}${timeBadge}</div>
			</div>
			<div class="game-card-actions">
				${infoOrEdit}
				${deleteBtn}
			</div>
		`;
	} else {
		// large (default)
		const coverHtml = game.coverImageUrl
			? `<img class="game-card-cover" src="${esc(game.coverImageUrl)}" alt="${esc(game.name)}" loading="lazy" />`
			: `<div class="game-card-cover-placeholder">&#127921;</div>`;

		const tags = [...(game.categories || []).slice(0, 3), ...(game.mechanics || []).slice(0, 2)]
			.map(t => `<span class="tag">${esc(t)}</span>`).join('');

		card.className = 'game-card game-card--large';
		card.innerHTML = `
			${coverHtml}
			<div class="game-card-body">
				<div class="game-card-title">${esc(game.name)}</div>
				${game.year ? `<div class="game-card-year">${game.year}</div>` : ''}
				<div class="game-card-meta">${bggBadge}${ratingBadge}${playersBadge}${timeBadge}</div>
				${tags ? `<div class="game-card-categories">${tags}</div>` : ''}
			</div>
			<div class="game-card-actions">
				${infoOrEdit}
				${deleteBtn}
			</div>
		`;
	}

	if (game.isBggSourced) {
		card.querySelector('.btn-info').addEventListener('click', () => openModal(game.id, true));
	} else {
		card.querySelector('.btn-edit').addEventListener('click', () => openModal(game.id, false));
	}
	card.querySelector('.btn-delete').addEventListener('click', () => confirmDelete(game));

	return card;
}

// ── BGG Dropdown ───────────────────────────────────────────
function renderBggResults(results) {
	bggResults.innerHTML = '';
	if (!results.length) {
		bggResults.innerHTML = '<li class="dropdown-empty">No results found.</li>';
	} else {
		results.forEach(r => {
			const li = document.createElement('li');
			li.innerHTML = `${esc(r.name)}<span class="year">${r.year ?? ''}</span>`;
			li.addEventListener('click', () => selectBggGame(r));
			bggResults.appendChild(li);
		});
	}
	bggResults.hidden = false;
}

function showBggLoading() {
	bggResults.innerHTML = '<li class="dropdown-loading">Searching BGG\u2026</li>';
	bggResults.hidden = false;
}

function hideBggResults() {
	bggResults.hidden = true;
	bggResults.innerHTML = '';
}

async function selectBggGame(result) {
	hideBggResults();
	bggSearchInput.value = '';

	const preview = await fetchBggPreview(result.bggId);
	if (!preview) { alert('Could not load game details from BGG.'); return; }

	openModal(null, false, preview);
}

// ── Cover Image Preview ────────────────────────────────────
function updateCoverPreview(url) {
	const container = document.getElementById('cover-preview');
	if (!url) {
		container.textContent = '';
		container.innerHTML = '&#127921;';
		container.className = 'cover-preview-box cover-preview-empty';
		return;
	}
	const img = document.createElement('img');
	img.src = url;
	img.alt = 'Cover preview';
	img.className = 'cover-preview-box';
	img.onerror = () => {
		container.innerHTML = '&#127921;';
		container.className = 'cover-preview-box cover-preview-empty';
	};
	container.className = '';
	container.innerHTML = '';
	container.appendChild(img);
}

// ── Modal ──────────────────────────────────────────────────
// gameId  = string UUID when opening an existing game (edit or info)
// readOnly = true  → info view for BGG-sourced games
// bggPreview = object when opening from BGG search result (new add)
function openModal(gameId = null, readOnly = false, bggPreview = null) {
	gameForm.reset();
	document.getElementById('form-id').value = '';
	document.getElementById('form-bgg-id').value = '';
	updateCoverPreview('');

	const saveBtn   = document.getElementById('btn-save');
	const bggLink   = document.getElementById('modal-bgg-link');

	// Always reset to editable state first
	gameForm.querySelectorAll('input, textarea').forEach(el => el.removeAttribute('readonly'));
	gameForm.querySelectorAll('select').forEach(el => el.removeAttribute('disabled'));
	saveBtn.style.display = '';
	bggLink.style.display = 'none';
	bggLink.href = '#';

	if (gameId !== null && typeof gameId === 'string') {
		// Edit or Info — look up game from allGames
		const game = allGames.find(g => g.id === gameId);
		if (!game) return;

		document.getElementById('form-id').value          = game.id;
		document.getElementById('form-bgg-id').value      = game.bggId ?? '';
		document.getElementById('form-name').value        = game.name;
		document.getElementById('form-year').value        = game.year ?? '';
		document.getElementById('form-bgg-rating').value  = game.bggRating ?? '';
		document.getElementById('form-min-players').value = game.minPlayers ?? '';
		document.getElementById('form-max-players').value = game.maxPlayers ?? '';
		document.getElementById('form-play-time').value   = game.playTimeMinutes ?? '';
		document.getElementById('form-cover').value       = game.coverImageUrl ?? '';
		updateCoverPreview(game.coverImageUrl ?? '');
		document.getElementById('form-categories').value  = (game.categories || []).join(', ');
		document.getElementById('form-mechanics').value   = (game.mechanics  || []).join(', ');
		document.getElementById('form-description').value = game.description ?? '';

		if (readOnly) {
			modalTitle.textContent = game.name;
			gameForm.querySelectorAll('input, textarea').forEach(el => el.setAttribute('readonly', ''));
			gameForm.querySelectorAll('select').forEach(el => el.setAttribute('disabled', ''));
			saveBtn.style.display = 'none';
			if (game.bggId) {
				bggLink.href = `https://boardgamegeek.com/boardgame/${game.bggId}`;
				bggLink.style.display = '';
			}
		} else {
			modalTitle.textContent = 'Edit Game';
		}
	} else if (bggPreview) {
		modalTitle.textContent = 'Add from BGG';
		document.getElementById('form-bgg-id').value      = bggPreview.bggId ?? '';
		document.getElementById('form-name').value        = bggPreview.name ?? '';
		document.getElementById('form-year').value        = bggPreview.year ?? '';
		document.getElementById('form-bgg-rating').value  = bggPreview.bggRating ?? '';
		document.getElementById('form-min-players').value = bggPreview.minPlayers ?? '';
		document.getElementById('form-max-players').value = bggPreview.maxPlayers ?? '';
		document.getElementById('form-play-time').value   = bggPreview.playTimeMinutes ?? '';
		document.getElementById('form-cover').value       = bggPreview.coverImageUrl ?? '';
		updateCoverPreview(bggPreview.coverImageUrl ?? '');
		document.getElementById('form-categories').value  = (bggPreview.categories || []).join(', ');
		document.getElementById('form-mechanics').value   = (bggPreview.mechanics  || []).join(', ');
		document.getElementById('form-description').value = bggPreview.description ?? '';
	} else {
		modalTitle.textContent = 'Add Game Manually';
	}

	modalOverlay.classList.remove('hidden');
	document.getElementById('form-name').focus();
}

function closeModal() {
	modalOverlay.classList.add('hidden');

	// Reset readonly state so re-opening in edit mode works correctly
	gameForm.querySelectorAll('input, textarea').forEach(el => el.removeAttribute('readonly'));
	gameForm.querySelectorAll('select').forEach(el => el.removeAttribute('disabled'));
	const saveBtn = document.getElementById('btn-save');
	saveBtn.style.display = '';
	const bggLink = document.getElementById('modal-bgg-link');
	bggLink.style.display = 'none';
	bggLink.href = '#';
}

async function handleFormSubmit(e) {
	e.preventDefault();

	const name = document.getElementById('form-name').value.trim();
	if (!name) { document.getElementById('form-name').focus(); return; }

	const id    = document.getElementById('form-id').value || null;
	const bggId = parseInt(document.getElementById('form-bgg-id').value) || null;

	const splitTags = val => val.split(',').map(s => s.trim()).filter(Boolean);

	const payload = {
		name,
		year:            intOrNull('form-year'),
		description:     document.getElementById('form-description').value.trim() || null,
		minPlayers:      intOrNull('form-min-players'),
		maxPlayers:      intOrNull('form-max-players'),
		playTimeMinutes: intOrNull('form-play-time'),
		bggRating:       floatOrNull('form-bgg-rating'),
		coverImageUrl:   document.getElementById('form-cover').value.trim() || null,
		categories:      splitTags(document.getElementById('form-categories').value),
		mechanics:       splitTags(document.getElementById('form-mechanics').value),
	};

	const saveBtn = document.getElementById('btn-save');
	setButtonLoading(saveBtn, 'Saving\u2026');

	try {
		if (id) {
			await saveGame(payload, id);
		} else if (bggId) {
			await saveGameFromBgg({ ...payload, bggId });
		} else {
			await saveGame(payload, null);
		}
		closeModal();
		await loadGames();
	} catch (err) {
		showInlineError(saveBtn, err.message);
	} finally {
		clearButtonLoading(saveBtn);
	}
}

// ── Delete Confirm ─────────────────────────────────────────
function confirmDelete(game) {
	deleteTargetId = game.id;
	confirmMessage.textContent = `Remove "${game.name}" from your collection?`;
	confirmOverlay.classList.remove('hidden');
}

// ── Helpers ────────────────────────────────────────────────
function esc(str) {
	return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function intOrNull(id) {
	const v = parseInt(document.getElementById(id).value);
	return isNaN(v) ? null : v;
}

function floatOrNull(id) {
	const v = parseFloat(document.getElementById(id).value);
	return isNaN(v) ? null : v;
}
