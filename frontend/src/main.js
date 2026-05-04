import { DEFAULT_SORT, VIEW_MODES, sortCriteria, setSortCriteria, setCurrentViewMode, currentViewMode, allGames } from './state.js';
import { loadConfig, loadGames } from './api.js';
import { setButtonLoading, clearButtonLoading, showSyncError } from './helpers.js';
import { applyBggAvailability } from './bgg.js';
import { renderGames } from './render.js';
import { renderSortBar, saveSort, toggleSortDropdown, hideSortDropdown, SORT_FIELDS } from './sort.js';
import { openModal, closeModal, handleFormSubmit, updateCoverPreview, confirmDelete, handleConfirmDelete } from './modal.js';
import { hideBggResults, showBggLoading, renderBggResults, selectBggGame } from './bgg.js';
import { searchBgg } from './api.js';
import { bggReachable, bggConfigured, setBggSearchTimer, bggSearchTimer, coverPreviewTimer, setCoverPreviewTimer } from './state.js';
import { API } from './api.js';

// ── DOM refs ───────────────────────────────────────────────
const filterInput    = document.getElementById('filter-input');
const bggSearchInput = document.getElementById('bgg-search-input');
const bggResults     = document.getElementById('bgg-search-results');

// ── Helpers ────────────────────────────────────────────────
function getFilter() {
	return filterInput.value.trim().toLowerCase();
}

	function refreshGames() {
	renderGames(getFilter(), {
		onEdit:   id => openModal(id, false, null, { onRefresh: () => loadGames(refreshGames) }),
		onDelete: game => confirmDelete(game),
		onInfo:   id => openModal(id, true, null, { onRefresh: () => loadGames(refreshGames) }),
	});
}

// ── View Mode ──────────────────────────────────────────────
function applyViewMode(mode) {
	const gameGrid = document.getElementById('game-grid');
	const nextMode = VIEW_MODES.includes(mode) ? mode : 'large';
	setCurrentViewMode(nextMode);
	localStorage.setItem('bgci-view-mode', nextMode);

	VIEW_MODES.forEach(m => gameGrid.classList.remove(`game-grid--${m}`));
	gameGrid.classList.add(`game-grid--${nextMode}`);

	document.querySelectorAll('.view-toggle-btn').forEach(btn => {
		btn.classList.toggle('active', btn.dataset.view === nextMode);
	});
}

// ── Event binding ──────────────────────────────────────────
function bindEvents(config) {
	document.getElementById('btn-add-manual').addEventListener('click', () => openModal());
	document.getElementById('modal-close').addEventListener('click', closeModal);
	document.getElementById('btn-cancel').addEventListener('click', closeModal);

	document.getElementById('game-form').addEventListener('submit', e =>
		handleFormSubmit(e, { onSaved: () => loadGames(refreshGames) })
	);

	filterInput.addEventListener('input', refreshGames);

	// BGG search input — always bound; the input itself is disabled when unavailable/unconfigured
	bggSearchInput.addEventListener('input', () => {
		if (!bggReachable || !bggConfigured) return;
		clearTimeout(bggSearchTimer);
		const q = bggSearchInput.value.trim();
		if (q.length < 2) { hideBggResults(); return; }
		setBggSearchTimer(setTimeout(async () => {
			showBggLoading();
			try {
				const data = await searchBgg(q);
				const allGameBggIds = new Set(allGames.filter(g => g.bggId).map(g => g.bggId));
				const filtered = data.filter(r => !allGameBggIds.has(r.bggId));
				renderBggResults(filtered, {
					onSelect: result => selectBggGame(result, {
						onOpenModal: (id, readOnly, preview) => openModal(id, readOnly, preview, { onRefresh: () => loadGames(refreshGames) }),
					}),
				});
			} catch {
				hideBggResults();
			}
		}, 400));
	});

	document.addEventListener('click', e => {
		if (!bggResults.contains(e.target) && e.target !== bggSearchInput) hideBggResults();
	});

	document.getElementById('form-cover').addEventListener('input', e => {
		clearTimeout(coverPreviewTimer);
		const url = e.target.value.trim();
		setCoverPreviewTimer(setTimeout(() => updateCoverPreview(url), 400));
	});

	document.getElementById('btn-confirm-cancel').addEventListener('click', () => {
		document.getElementById('confirm-overlay').classList.add('hidden');
		setDeleteTargetId(null);
	});

	document.getElementById('btn-confirm-delete').addEventListener('click', () =>
		handleConfirmDelete({ onDeleted: () => loadGames(refreshGames) })
	);

	// View toggle
	document.getElementById('view-toggle').addEventListener('click', e => {
		const btn = e.target.closest('.view-toggle-btn');
		if (!btn) return;
		applyViewMode(btn.dataset.view);
		refreshGames();
	});

	// Sync BGG button
	const syncBtn = document.getElementById('btn-sync-bgg');
	syncBtn.addEventListener('click', async () => {
		setButtonLoading(syncBtn, 'Syncing\u2026');
		try {
			const res = await fetch(`${API}/sync/bgg`, { method: 'POST' });
			if (!res.ok) throw new Error(await res.text());
			await loadGames(refreshGames);
		} catch (err) {
			showSyncError(err.message || 'Sync failed.');
		} finally {
			clearButtonLoading(syncBtn);
		}
	});

	// Sort bar — add button
	document.getElementById('btn-sort-add').addEventListener('click', e => {
		e.stopPropagation();
		toggleSortDropdown(() => {
			refreshGames();
		});
	});

	// Sort bar — clear button
	document.getElementById('btn-sort-clear').addEventListener('click', () => {
		setSortCriteria([...DEFAULT_SORT]);
		saveSort();
		renderSortBar();
		refreshGames();
	});

	// Sort bar — chip interactions (delegated)
	document.getElementById('sort-chips').addEventListener('click', e => {
		const dirBtn    = e.target.closest('.sort-chip-dir');
		const removeBtn = e.target.closest('.sort-chip-remove');
		if (dirBtn) {
			const field = dirBtn.closest('.sort-chip').dataset.field;
			const c = sortCriteria.find(x => x.field === field);
			if (c) {
				c.dir = c.dir === 'asc' ? 'desc' : 'asc';
				saveSort();
				renderSortBar();
				refreshGames();
			}
		} else if (removeBtn) {
			const field = removeBtn.closest('.sort-chip').dataset.field;
			let next = sortCriteria.filter(x => x.field !== field);
			if (next.length === 0) next = [...DEFAULT_SORT];
			setSortCriteria(next);
			saveSort();
			renderSortBar();
			refreshGames();
		}
	});

	// Close sort dropdown on outside click
	document.addEventListener('click', () => hideSortDropdown());
}

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
			const sanitized = parsed.filter(c =>
				SORT_FIELDS.some(f => f.field === c.field) && (c.dir === 'asc' || c.dir === 'desc')
			);
			setSortCriteria(sanitized.length > 0 ? sanitized : [...DEFAULT_SORT]);
		}
	} catch { setSortCriteria([...DEFAULT_SORT]); }
	renderSortBar();

	const config = await loadConfig();
	if (config.version) {
		const badge = document.getElementById('app-version');
		if (badge) badge.textContent = (config.version === 'dev' || config.version === 'latest') ? config.version : `v${config.version}`;
	}
	applyBggAvailability(config, { onRenderGames: refreshGames });
	if (config.bggSyncEnabled) {
		document.getElementById('btn-sync-bgg').style.display = '';
	}
	await loadGames(refreshGames);
	bindEvents(config);
});
