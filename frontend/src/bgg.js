import {
	bggReachable, bggConfigured, bggPollInterval,
	setBggReachable, setBggConfigured, setBggPollInterval,
} from './state.js';
import { loadConfig } from './api.js';
import { fetchBggPreview } from './api.js';
import { esc } from './helpers.js';

// ── BGG Availability ───────────────────────────────────────

// Single entry point for all BGG-related UI state.
// Called on initial load and on each polling check.
// onRenderGames() — callback to re-render cards when reachability changes
export function applyBggAvailability(config, { onRenderGames } = {}) {
	const wasReachable = bggReachable;
	setBggReachable(config.bggReachable ?? false);
	setBggConfigured(config.bggConfigured ?? false);

	initBggSearch(config);
	applyBggBanner(bggReachable);

	// Enable/disable the sync button based on current reachability
	const syncBtn = document.getElementById('btn-sync-bgg');
	if (syncBtn) syncBtn.disabled = !bggReachable;

	// Re-render cards so BGG badge links reflect current reachability
	if (wasReachable !== bggReachable && onRenderGames) {
		onRenderGames();
	}

	// Start or stop polling
	if (!bggReachable && !bggPollInterval) {
		setBggPollInterval(setInterval(() => checkBggStatus({ onRenderGames }), 2 * 60 * 1000));
	} else if (bggReachable && bggPollInterval) {
		clearInterval(bggPollInterval);
		setBggPollInterval(null);
	}
}

export async function checkBggStatus({ onRenderGames } = {}) {
	const config = await loadConfig();
	applyBggAvailability(config, { onRenderGames });
}

export function applyBggBanner(reachable) {
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
export function initBggSearch(config) {
	const bggSearchInput = document.getElementById('bgg-search-input');
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

// ── BGG Search Dropdown ────────────────────────────────────

// onSelect(result) — callback so main.js can wire selectBggGame with its own context
export function renderBggResults(results, { onSelect } = {}) {
	const bggResults = document.getElementById('bgg-search-results');
	bggResults.innerHTML = '';
	if (!results.length) {
		bggResults.innerHTML = '<li class="dropdown-empty">No results found.</li>';
	} else {
		results.forEach(r => {
			const li = document.createElement('li');
			li.innerHTML = `${esc(r.name)}<span class="year">${r.year ?? ''}</span>`;
			li.addEventListener('click', () => onSelect && onSelect(r));
			bggResults.appendChild(li);
		});
	}
	bggResults.hidden = false;
}

export function showBggLoading() {
	const bggResults = document.getElementById('bgg-search-results');
	bggResults.innerHTML = '<li class="dropdown-loading">Searching BGG\u2026</li>';
	bggResults.hidden = false;
}

export function hideBggResults() {
	const bggResults = document.getElementById('bgg-search-results');
	bggResults.hidden = true;
	bggResults.innerHTML = '';
}

// onOpenModal(null, false, preview) — callback to open the modal pre-filled with BGG preview data
export async function selectBggGame(result, { onOpenModal } = {}) {
	hideBggResults();
	document.getElementById('bgg-search-input').value = '';

	const preview = await fetchBggPreview(result.bggId);
	if (!preview) { alert('Could not load game details from BGG.'); return; }

	if (onOpenModal) onOpenModal(null, false, preview);
}
