import { allGames, deleteTargetId, setDeleteTargetId } from './state.js';
import { setButtonLoading, clearButtonLoading, showInlineError, intOrNull, floatOrNull } from './helpers.js';
import { saveGame, saveGameFromBgg, deleteGame } from './api.js';
import { renderExpansionsTab } from './expansions.js';

// ── DOM refs ───────────────────────────────────────────────
const modalOverlay      = document.getElementById('modal-overlay');
const modalTitle        = document.getElementById('modal-title');
const gameForm          = document.getElementById('game-form');
const confirmOverlay    = document.getElementById('confirm-overlay');
const confirmMessage    = document.getElementById('confirm-message');
const modalTabs         = document.getElementById('modal-tabs');
const expansionsContent = document.getElementById('expansions-tab-content');

// Current game for the expansions tab
let _currentGame = null;
let _tabRefresh  = null;

// ── Tab switching ──────────────────────────────────────────
function activateTab(tab) {
	modalTabs.querySelectorAll('.modal-tab-btn').forEach(btn => {
		btn.classList.toggle('active', btn.dataset.tab === tab);
	});
	if (tab === 'details') {
		gameForm.classList.remove('hidden');
		expansionsContent.classList.add('hidden');
	} else {
		gameForm.classList.add('hidden');
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

// ── Modal ──────────────────────────────────────────────────
// gameId     = string UUID when opening an existing game (edit or info)
// readOnly   = true → info view for BGG-sourced games
// bggPreview = object when opening from BGG search result (new add)
// onRefresh  = callback to reload games after an expansion is added/removed
export function openModal(gameId = null, readOnly = false, bggPreview = null, { onRefresh } = {}) {
	gameForm.reset();
	document.getElementById('form-id').value = '';
	document.getElementById('form-bgg-id').value = '';
	updateCoverPreview('');

	// Reset tab state — clear stale expansion content and default to Details
	_currentGame = null;
	_tabRefresh  = onRefresh ?? null;
	expansionsContent.innerHTML = '';
	expansionsContent.classList.add('hidden');
	gameForm.classList.remove('hidden');
	modalTabs.classList.add('hidden');
	modalTabs.querySelectorAll('.modal-tab-btn').forEach(btn => {
		btn.classList.toggle('active', btn.dataset.tab === 'details');
	});

	const saveBtn = document.getElementById('btn-save');
	const bggLink = document.getElementById('modal-bgg-link');

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

		_currentGame = game;

		// Show Expansions tab only for top-level games (not for expansions themselves)
		if (!game.parentGameId) {
			modalTabs.classList.remove('hidden');
		}

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

export function closeModal() {
	modalOverlay.classList.add('hidden');

	// Reset readonly state so re-opening in edit mode works correctly
	gameForm.querySelectorAll('input, textarea').forEach(el => el.removeAttribute('readonly'));
	gameForm.querySelectorAll('select').forEach(el => el.removeAttribute('disabled'));
	const saveBtn = document.getElementById('btn-save');
	saveBtn.style.display = '';
	const bggLink = document.getElementById('modal-bgg-link');
	bggLink.style.display = 'none';
	bggLink.href = '#';

	// Reset tab state and clear stale content
	_currentGame = null;
	_tabRefresh  = null;
	expansionsContent.innerHTML = '';
	expansionsContent.classList.add('hidden');
	gameForm.classList.remove('hidden');
	modalTabs.classList.add('hidden');
	modalTabs.querySelectorAll('.modal-tab-btn').forEach(btn => {
		btn.classList.toggle('active', btn.dataset.tab === 'details');
	});
}

// onSaved() — callback invoked after a successful save so main.js can refresh the game list
export async function handleFormSubmit(e, { onSaved } = {}) {
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

// onDeleted() — callback invoked after successful deletion so main.js can refresh the game list
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
