import { allGames, deleteTargetId, setDeleteTargetId } from './state.js';
import { setButtonLoading, clearButtonLoading, showInlineError, intOrNull, floatOrNull, esc } from './helpers.js';
import { saveGame, saveGameFromBgg, deleteGame } from './api.js';
import { renderExpansionsTab } from './expansions.js';

// ── DOM refs ───────────────────────────────────────────────
const modalOverlay      = document.getElementById('modal-overlay');
const modalEl           = modalOverlay.querySelector('.modal');
const modalTitle        = document.getElementById('modal-title');
const gameForm          = document.getElementById('game-form');
const confirmOverlay    = document.getElementById('confirm-overlay');
const confirmMessage    = document.getElementById('confirm-message');
const modalTabs         = document.getElementById('modal-tabs');
const modalInfoHero     = document.getElementById('modal-info-hero');
const expansionsContent = document.getElementById('expansions-tab-content');
const gameInfoPanel     = document.getElementById('game-info-panel');

// Current game / tab state
let _currentGame = null;
let _tabRefresh  = null;
let _readOnly    = false;

// ── Tab switching ──────────────────────────────────────────
function activateTab(tab) {
	modalTabs.querySelectorAll('.modal-tab-btn').forEach(btn => {
		btn.classList.toggle('active', btn.dataset.tab === tab);
	});
	if (tab === 'info') {
		expansionsContent.classList.add('hidden');
		if (_readOnly) {
			gameInfoPanel.classList.remove('hidden');
		} else {
			gameForm.classList.remove('hidden');
		}
	} else {
		gameForm.classList.add('hidden');
		gameInfoPanel.classList.add('hidden');
		expansionsContent.classList.remove('hidden');
		if (_currentGame) {
			renderExpansionsTab(_currentGame, { onRefresh: _tabRefresh });
		}
	}
}

// Bind tab buttons via delegation (safe to call at module load time)
modalTabs.addEventListener('click', e => {
	const btn = e.target.closest('.modal-tab-btn');
	if (!btn) return;
	activateTab(btn.dataset.tab);
});

// ── Cover Image Preview ────────────────────────────────────
export function updateCoverPreview(url) {
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

// ── Info panel helpers ─────────────────────────────────────
const DIFFICULTY_LEVELS = [
	{ max: 1.5, label: 'Apprentice',  color: '#9e9e9e' },
	{ max: 2.4, label: 'Journeyman',  color: '#4caf50' },
	{ max: 3.4, label: 'Adept',       color: '#2196f3' },
	{ max: 4.2, label: 'Expert',      color: '#9c27b0' },
	{ max: 5.0, label: 'Grandmaster', color: '#ff9800' },
];

function diffBadgeHtml(w) {
	if (!w) return '';
	const level = DIFFICULTY_LEVELS.find(l => w <= l.max) ?? DIFFICULTY_LEVELS[DIFFICULTY_LEVELS.length - 1];
	return `<span class="badge badge-difficulty" style="--diff-color:${level.color}">${level.label}</span>`;
}

function recommendedPlayersBadgeHtml(bestMin, bestMax) {
	if (!bestMin) return '';
	const range = !bestMax
		? `${bestMin}p`
		: bestMax >= 99
			? `${bestMin}+`
			: `${bestMin}\u2013${bestMax}p`;
	return `<span class="badge badge-best-players">&#9733; Best ${range}</span>`;
}

// Hero block shown above the tab bar (cover + title + badges)
function buildInfoHero(game) {
	const coverHtml = game.coverImageUrl
		? `<img class="info-hero-cover" src="${esc(game.coverImageUrl)}" alt="${esc(game.name)}" />`
		: `<div class="info-hero-cover info-hero-cover--placeholder">&#127921;</div>`;

	const playersPart = game.minPlayers
		? `&#128100; ${game.minPlayers === game.maxPlayers ? game.minPlayers : `${game.minPlayers}\u2013${game.maxPlayers}`}`
		: '';

	const timePart = (() => {
		const min = game.minPlayTimeMinutes, max = game.maxPlayTimeMinutes, pt = game.playTimeMinutes;
		if (min && max && min !== max) return `&#9201; ${min}\u2013${max} min`;
		const m = max || min || pt;
		return m ? `&#9201; ${m} min` : '';
	})();

	const ratingBadge     = game.bggRating    ? `<span class="badge badge-rating">&#9733; ${Number(game.bggRating).toFixed(1)}</span>` : '';
	const playersBadge    = playersPart        ? `<span class="badge badge-players">${playersPart}</span>` : '';
	const timeBadge       = timePart           ? `<span class="badge">${timePart}</span>` : '';
	const diffBadge       = diffBadgeHtml(game.bggWeight);
	const ageBadge        = game.minAge        ? `<span class="badge badge-age">${game.minAge}+</span>` : '';
	const bestPlayersBadge = recommendedPlayersBadgeHtml(game.bestPlayerCountMin, game.bestPlayerCountMax);
	const expansionsBadge = game.expansionCount > 0
		? `<span class="badge badge-expansions">+${game.expansionCount} expansion${game.expansionCount === 1 ? '' : 's'}</span>`
		: '';

	return `
		<div class="info-hero">
			${coverHtml}
			<div class="info-hero-body">
				<div class="info-hero-title">${esc(game.name)}${game.year ? ` <span class="info-hero-year">(${game.year})</span>` : ''}${game.bggRating ? ` <span class="info-hero-rating">&#9733; ${Number(game.bggRating).toFixed(1)}</span>` : ''}</div>
				<div class="info-hero-badges">
					${playersBadge}${timeBadge}${ageBadge}${diffBadge}${bestPlayersBadge}${expansionsBadge}
				</div>
			</div>
		</div>
	`;
}

// Panel content shown in the Info tab (below the tab bar)
function buildInfoPanel(game) {
	// Three-column content: categories, mechanics, credits
	const categoryTags = (game.categories || [])
		.map(t => `<span class="tag">${esc(t)}</span>`).join('');
	const mechanicTags = (game.mechanics || [])
		.map(t => `<span class="tag">${esc(t)}</span>`).join('');

	const creditRows = [];
	if ((game.designers  || []).length > 0) {
		creditRows.push(`<span class="info-credits-label">Designers</span>`);
		game.designers.forEach(d => creditRows.push(`<div class="credits-name">${esc(d)}</div>`));
	}
	if ((game.artists || []).length > 0) {
		creditRows.push(`<span class="info-credits-label">Artists</span>`);
		game.artists.forEach(a => creditRows.push(`<div class="credits-name">${esc(a)}</div>`));
	}
	if ((game.publishers || []).length > 0) {
		creditRows.push(`<span class="info-credits-label">Publishers</span>`);
		game.publishers.forEach(p => creditRows.push(`<div class="credits-name">${esc(p)}</div>`));
	}

	const subdomainHtml = (game.subdomains || []).length > 0
		? `<div class="info-subdomains">${game.subdomains.map(s => `<span class="tag tag-subdomain">${esc(s)}</span>`).join('')}</div>`
		: '';

	const footerBtn = game.isBggSourced
		? `<a class="btn btn-secondary" href="https://boardgamegeek.com/boardgame/${game.bggId}" target="_blank" rel="noopener" style="text-decoration:none">&#128279; View on BGG</a>`
		: `<button class="btn btn-secondary btn-edit-from-info">&#9998; Edit</button>`;

	return `
		${game.description
			? `<div class="info-description-box">${esc(game.description)}</div>`
			: ''}
		<div class="info-columns">
			<div class="info-column">
				<div class="info-column-header">Categories</div>
				<div class="info-column-tags">${categoryTags}</div>
			</div>
			<div class="info-column">
				<div class="info-column-header">Mechanics</div>
				<div class="info-column-tags">${mechanicTags}</div>
			</div>
			<div class="info-column">
				<div class="info-column-header">Credits</div>
				<div class="info-column-tags">${creditRows.join('')}</div>
			</div>
		</div>
		${subdomainHtml}
		<div class="info-footer">
			${footerBtn}
		</div>
	`;
}

// ── Helpers to show / hide info-mode chrome ────────────────
function showInfoChrome(game, { onEdit } = {}) {
	modalInfoHero.innerHTML = buildInfoHero(game);
	modalInfoHero.classList.remove('hidden');
	modalTitle.style.display = 'none';
}

function hideInfoChrome() {
	modalInfoHero.classList.add('hidden');
	modalInfoHero.innerHTML = '';
	modalTitle.style.display = '';
}

// ── Modal ──────────────────────────────────────────────────
export function openModal(gameId = null, readOnly = false, bggPreview = null, { onRefresh } = {}) {
	gameForm.reset();
	document.getElementById('form-id').value = '';
	document.getElementById('form-bgg-id').value = '';
	updateCoverPreview('');

	// Reset state
	_currentGame = null;
	_tabRefresh  = onRefresh ?? null;
	_readOnly    = readOnly;
	expansionsContent.innerHTML = '';
	expansionsContent.classList.add('hidden');
	gameInfoPanel.classList.add('hidden');
	gameInfoPanel.innerHTML = '';
	gameForm.classList.remove('hidden');
	modalTabs.classList.add('hidden');
	modalTabs.querySelectorAll('.modal-tab-btn').forEach(btn => {
		btn.classList.toggle('active', btn.dataset.tab === 'info');
	});
	hideInfoChrome();

	const saveBtn = document.getElementById('btn-save');

	// Always reset to editable state
	gameForm.querySelectorAll('input, textarea').forEach(el => el.removeAttribute('readonly'));
	gameForm.querySelectorAll('select').forEach(el => el.removeAttribute('disabled'));
	saveBtn.style.display = '';

	if (gameId !== null && typeof gameId === 'string') {
		const game = allGames.find(g => g.id === gameId);
		if (!game) return;

		_currentGame = game;

		if (!game.parentGameId) {
			modalTabs.classList.remove('hidden');
		}

		if (readOnly) {
			// Info view — hero above tabs + panel below
			gameForm.classList.add('hidden');
			showInfoChrome(game);
			gameInfoPanel.innerHTML = buildInfoPanel(game);
			gameInfoPanel.classList.remove('hidden');

			// Wire Edit button for manually-added games
			const editFromInfo = gameInfoPanel.querySelector('.btn-edit-from-info');
			if (editFromInfo) {
				editFromInfo.addEventListener('click', () => openModal(gameId, false, null, { onRefresh }));
			}
		} else {
			// Edit view — fill form
			modalTitle.textContent = 'Edit Game';
			document.getElementById('form-id').value              = game.id;
			document.getElementById('form-bgg-id').value          = game.bggId ?? '';
			document.getElementById('form-name').value            = game.name;
			document.getElementById('form-year').value            = game.year ?? '';
			document.getElementById('form-bgg-rating').value      = game.bggRating ?? '';
			document.getElementById('form-bgg-weight').value      = game.bggWeight ?? '';
			document.getElementById('form-min-age').value         = game.minAge ?? '';
			document.getElementById('form-min-players').value     = game.minPlayers ?? '';
			document.getElementById('form-max-players').value     = game.maxPlayers ?? '';
			document.getElementById('form-best-player-count-min').value = game.bestPlayerCountMin ?? '';
			document.getElementById('form-best-player-count-max').value = game.bestPlayerCountMax ?? '';
			document.getElementById('form-play-time').value       = game.playTimeMinutes ?? '';
			document.getElementById('form-min-play-time').value   = game.minPlayTimeMinutes ?? '';
			document.getElementById('form-max-play-time').value   = game.maxPlayTimeMinutes ?? '';
			document.getElementById('form-cover').value           = game.coverImageUrl ?? '';
			updateCoverPreview(game.coverImageUrl ?? '');
			document.getElementById('form-categories').value      = (game.categories  || []).join(', ');
			document.getElementById('form-mechanics').value       = (game.mechanics   || []).join(', ');
			document.getElementById('form-designers').value       = (game.designers   || []).join(', ');
			document.getElementById('form-artists').value         = (game.artists     || []).join(', ');
			document.getElementById('form-publishers').value      = (game.publishers  || []).join(', ');
			document.getElementById('form-subdomains').value      = (game.subdomains  || []).join(', ');
			document.getElementById('form-description').value     = game.description ?? '';
		}
	} else if (bggPreview) {
		modalTitle.textContent = 'Add from BGG';
		document.getElementById('form-bgg-id').value          = bggPreview.bggId ?? '';
		document.getElementById('form-name').value            = bggPreview.name ?? '';
		document.getElementById('form-year').value            = bggPreview.year ?? '';
		document.getElementById('form-bgg-rating').value      = bggPreview.bggRating ?? '';
		document.getElementById('form-bgg-weight').value      = bggPreview.bggWeight ?? '';
		document.getElementById('form-min-age').value         = bggPreview.minAge ?? '';
		document.getElementById('form-min-players').value     = bggPreview.minPlayers ?? '';
		document.getElementById('form-max-players').value     = bggPreview.maxPlayers ?? '';
		document.getElementById('form-best-player-count-min').value = bggPreview.bestPlayerCountMin ?? '';
		document.getElementById('form-best-player-count-max').value = bggPreview.bestPlayerCountMax ?? '';
		document.getElementById('form-play-time').value       = bggPreview.playTimeMinutes ?? '';
		document.getElementById('form-min-play-time').value   = bggPreview.minPlayTimeMinutes ?? '';
		document.getElementById('form-max-play-time').value   = bggPreview.maxPlayTimeMinutes ?? '';
		document.getElementById('form-cover').value           = bggPreview.coverImageUrl ?? '';
		updateCoverPreview(bggPreview.coverImageUrl ?? '');
		document.getElementById('form-categories').value      = (bggPreview.categories  || []).join(', ');
		document.getElementById('form-mechanics').value       = (bggPreview.mechanics   || []).join(', ');
		document.getElementById('form-designers').value       = (bggPreview.designers   || []).join(', ');
		document.getElementById('form-artists').value         = (bggPreview.artists     || []).join(', ');
		document.getElementById('form-publishers').value      = (bggPreview.publishers  || []).join(', ');
		document.getElementById('form-subdomains').value      = (bggPreview.subdomains  || []).join(', ');
		document.getElementById('form-description').value     = bggPreview.description ?? '';
	} else {
		modalTitle.textContent = 'Add Game Manually';
	}

	modalOverlay.classList.remove('hidden');
	requestAnimationFrame(() => {
		modalEl.style.minHeight = modalEl.offsetHeight + 'px';
	});
	if (!readOnly) {
		document.getElementById('form-name').focus();
	}
}

export function closeModal() {
	modalOverlay.classList.add('hidden');
	modalEl.style.minHeight = '';

	gameForm.querySelectorAll('input, textarea').forEach(el => el.removeAttribute('readonly'));
	gameForm.querySelectorAll('select').forEach(el => el.removeAttribute('disabled'));
	document.getElementById('btn-save').style.display = '';

	_currentGame = null;
	_tabRefresh  = null;
	_readOnly    = false;
	expansionsContent.innerHTML = '';
	expansionsContent.classList.add('hidden');
	gameInfoPanel.classList.add('hidden');
	gameInfoPanel.innerHTML = '';
	gameForm.classList.remove('hidden');
	modalTabs.classList.add('hidden');
	modalTabs.querySelectorAll('.modal-tab-btn').forEach(btn => {
		btn.classList.toggle('active', btn.dataset.tab === 'info');
	});
	hideInfoChrome();
}

export async function handleFormSubmit(e, { onSaved } = {}) {
	e.preventDefault();

	const name = document.getElementById('form-name').value.trim();
	if (!name) { document.getElementById('form-name').focus(); return; }

	const id    = document.getElementById('form-id').value || null;
	const bggId = parseInt(document.getElementById('form-bgg-id').value) || null;

	const splitTags = val => val.split(',').map(s => s.trim()).filter(Boolean);

	const payload = {
		name,
		year:                intOrNull('form-year'),
		description:         document.getElementById('form-description').value.trim() || null,
		minPlayers:          intOrNull('form-min-players'),
		maxPlayers:          intOrNull('form-max-players'),
		playTimeMinutes:     intOrNull('form-play-time'),
		minPlayTimeMinutes:  intOrNull('form-min-play-time'),
		maxPlayTimeMinutes:  intOrNull('form-max-play-time'),
		bggRating:           floatOrNull('form-bgg-rating'),
		bggWeight:           floatOrNull('form-bgg-weight'),
		minAge:              intOrNull('form-min-age'),
		coverImageUrl:       document.getElementById('form-cover').value.trim() || null,
		categories:          splitTags(document.getElementById('form-categories').value),
		mechanics:           splitTags(document.getElementById('form-mechanics').value),
		designers:           splitTags(document.getElementById('form-designers').value),
		artists:             splitTags(document.getElementById('form-artists').value),
		publishers:          splitTags(document.getElementById('form-publishers').value),
		subdomains:          splitTags(document.getElementById('form-subdomains').value),
		bestPlayerCountMin:  intOrNull('form-best-player-count-min'),
		bestPlayerCountMax:  intOrNull('form-best-player-count-max'),
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
		if (onSaved) await onSaved();
	} catch (err) {
		showInlineError(saveBtn, err.message);
	} finally {
		clearButtonLoading(saveBtn);
	}
}

// ── Delete Confirm ─────────────────────────────────────────
export function confirmDelete(game) {
	setDeleteTargetId(game.id);
	confirmMessage.textContent = `Remove "${game.name}" from your collection?`;
	confirmOverlay.classList.remove('hidden');
}

export async function handleConfirmDelete({ onDeleted } = {}) {
	if (!deleteTargetId) return;
	const confirmBtn = document.getElementById('btn-confirm-delete');
	setButtonLoading(confirmBtn, 'Removing\u2026');
	try {
		await deleteGame(deleteTargetId, onDeleted);
		confirmOverlay.classList.add('hidden');
		setDeleteTargetId(null);
	} catch (err) {
		showInlineError(confirmBtn, err.message);
	} finally {
		clearButtonLoading(confirmBtn);
	}
}
