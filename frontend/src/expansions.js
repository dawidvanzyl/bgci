import { allGames, expansionSearchTimer, setExpansionSearchTimer, bggReachable } from './state.js';
import { fetchBggExpansions, saveExpansion, saveExpansionFromBgg, deleteGame, searchBgg } from './api.js';
import { esc, setButtonLoading, clearButtonLoading, showInlineError } from './helpers.js';

export async function renderExpansionsTab(game, { onRefresh } = {}) {
	const container = document.getElementById('expansions-tab-content');
	container.innerHTML = '';

	// ── Add expansion ──────────────────────────────────────
	const addSection = document.createElement('section');
	addSection.className = 'exp-section';

	const addTitle = document.createElement('h4');
	addTitle.className = 'exp-section-title';
	addTitle.textContent = 'Add Expansion';
	addSection.appendChild(addTitle);

	// Known BGG expansions dropdown (only for BGG-sourced games when reachable)
	if (game.bggId && bggReachable) {
		const knownWrap = document.createElement('div');
		knownWrap.className = 'exp-known-wrap';

		const knownLabel = document.createElement('label');
		knownLabel.className = 'exp-label';
		knownLabel.textContent = 'Known BGG expansions:';
		knownWrap.appendChild(knownLabel);

		const knownRow = document.createElement('div');
		knownRow.className = 'exp-known-row';

		const knownSelect = document.createElement('select');
		knownSelect.className = 'exp-known-select';
		knownSelect.innerHTML = '<option value="">Loading\u2026</option>';
		knownSelect.disabled = true;
		knownRow.appendChild(knownSelect);

		const knownAddBtn = document.createElement('button');
		knownAddBtn.type = 'button';
		knownAddBtn.className = 'btn btn-primary';
		knownAddBtn.textContent = 'Add';
		knownAddBtn.disabled = true;
		knownRow.appendChild(knownAddBtn);

		knownWrap.appendChild(knownRow);
		addSection.appendChild(knownWrap);

		// Load known expansions asynchronously
		fetchBggExpansions(game.bggId).then(expansions => {
			const allGameBggIds = new Set(allGames.filter(g => g.bggId).map(g => g.bggId));
			const available = (expansions || []).filter(e => !allGameBggIds.has(e.bggId));
			if (available.length === 0) {
				knownSelect.innerHTML = '<option value="">No new expansions found</option>';
			} else {
				knownSelect.innerHTML =
					'<option value="">\u2014 Select expansion \u2014</option>' +
					available.map(e => `<option value="${e.bggId}">${esc(e.name)}${e.year ? ` (${e.year})` : ''}</option>`).join('');
				knownSelect.disabled = false;
			}
		}).catch(() => {
			knownSelect.innerHTML = '<option value="">Failed to load</option>';
		});

		knownSelect.addEventListener('change', () => {
			knownAddBtn.disabled = !knownSelect.value;
		});

		knownAddBtn.addEventListener('click', async () => {
			const bggId = parseInt(knownSelect.value);
			if (!bggId) return;
			setButtonLoading(knownAddBtn, 'Adding\u2026');
			try {
				await saveExpansionFromBgg(bggId, game.id);
				if (onRefresh) await onRefresh();
				await renderExpansionsTab(game, { onRefresh });
			} catch (err) {
				showInlineError(knownAddBtn, err.message);
			} finally {
				clearButtonLoading(knownAddBtn);
			}
		});
	}

	// BGG search for expansions
	if (bggReachable) {
		const searchWrap = document.createElement('div');
		searchWrap.className = 'exp-search-wrap';

		const searchLabel = document.createElement('label');
		searchLabel.className = 'exp-label';
		searchLabel.textContent = 'Search BGG:';
		searchWrap.appendChild(searchLabel);

		const searchRow = document.createElement('div');
		searchRow.className = 'exp-search-row';

		const searchInput = document.createElement('input');
		searchInput.type = 'text';
		searchInput.placeholder = 'Search BGG for an expansion\u2026';
		searchInput.className = 'exp-search-input';
		searchRow.appendChild(searchInput);

		const searchStatus = document.createElement('span');
		searchStatus.className = 'exp-search-status';
		searchStatus.hidden = true;
		searchStatus.innerHTML = '<svg class="btn-spinner" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round"><path d="M12 2a10 10 0 0 1 10 10" /></svg>Adding\u2026';
		searchRow.appendChild(searchStatus);

		searchWrap.appendChild(searchRow);

		const searchResults = document.createElement('ul');
		searchResults.className = 'dropdown exp-search-results';
		searchResults.hidden = true;
		searchWrap.appendChild(searchResults);

		addSection.appendChild(searchWrap);

		searchInput.addEventListener('input', () => {
			clearTimeout(expansionSearchTimer);
			const q = searchInput.value.trim();
			if (q.length < 2) { searchResults.hidden = true; return; }
			setExpansionSearchTimer(setTimeout(async () => {
				try {
					const data = await searchBgg(q);
					const allGameBggIds = new Set(allGames.filter(g => g.bggId).map(g => g.bggId));
					const filtered = data.filter(r => !allGameBggIds.has(r.bggId));
					searchResults.innerHTML = filtered.length === 0
						? '<li class="dropdown-item dropdown-empty">No results</li>'
						: filtered.map(r =>
							`<li class="dropdown-item" data-bgg-id="${r.bggId}">${esc(r.name)}${r.year ? ` (${r.year})` : ''}</li>`
						).join('');
					searchResults.hidden = false;
				} catch {
					searchResults.hidden = true;
				}
			}, 400));
		});

		searchResults.addEventListener('click', async (e) => {
			const li = e.target.closest('.dropdown-item[data-bgg-id]');
			if (!li) return;
			const bggId = parseInt(li.dataset.bggId);
			searchResults.hidden = true;
			searchInput.value = li.textContent.trim();
			searchInput.disabled = true;
			searchStatus.hidden = false;
			try {
				await saveExpansionFromBgg(bggId, game.id);
				if (onRefresh) await onRefresh();
				await renderExpansionsTab(game, { onRefresh });
			} catch (err) {
				searchInput.disabled = false;
				searchStatus.hidden = true;
				alert(err.message);
			}
		});
	}

	// Manual add
	const manualWrap = document.createElement('div');
	manualWrap.className = 'exp-manual-wrap';

	const manualLabel = document.createElement('label');
	manualLabel.className = 'exp-label';
	manualLabel.textContent = 'Add manually:';
	manualWrap.appendChild(manualLabel);

	const manualRow = document.createElement('div');
	manualRow.className = 'exp-manual-row';

	const manualInput = document.createElement('input');
	manualInput.type = 'text';
	manualInput.placeholder = 'Expansion name\u2026';
	manualInput.className = 'exp-manual-input';
	manualRow.appendChild(manualInput);

	const manualAddBtn = document.createElement('button');
	manualAddBtn.type = 'button';
	manualAddBtn.className = 'btn btn-primary';
	manualAddBtn.textContent = 'Add';
	manualRow.appendChild(manualAddBtn);

	manualWrap.appendChild(manualRow);
	addSection.appendChild(manualWrap);
	container.appendChild(addSection);

	// ── Divider ────────────────────────────────────────────
	const divider = document.createElement('hr');
	divider.className = 'exp-divider';
	container.appendChild(divider);

	// ── Owned expansions ──────────────────────────────────
	const owned = allGames.filter(g => g.parentGameId === game.id);

	const ownedSection = document.createElement('section');
	ownedSection.className = 'exp-section';

	const ownedTitle = document.createElement('h4');
	ownedTitle.className = 'exp-section-title';
	ownedTitle.textContent = `Owned Expansions (${owned.length})`;
	ownedSection.appendChild(ownedTitle);

	if (owned.length === 0) {
		const empty = document.createElement('p');
		empty.className = 'exp-empty';
		empty.textContent = 'No expansions in your collection yet.';
		ownedSection.appendChild(empty);
	} else {
		const list = document.createElement('ul');
		list.className = 'exp-owned-list';
		owned.forEach(exp => {
			const li = document.createElement('li');
			li.className = 'exp-owned-item';
			li.innerHTML = `
				<span class="exp-owned-name">${esc(exp.name)}${exp.year ? ` <span class="exp-year">(${exp.year})</span>` : ''}</span>
				<button class="btn btn-danger btn-sm" data-id="${exp.id}">Remove</button>
			`;
			li.querySelector('button').addEventListener('click', async (e) => {
				const btn = e.currentTarget;
				setButtonLoading(btn, 'Removing\u2026');
				try {
					await deleteGame(exp.id);
					if (onRefresh) await onRefresh();
					await renderExpansionsTab(game, { onRefresh });
				} catch (err) {
					showInlineError(btn, err.message);
				} finally {
					clearButtonLoading(btn);
				}
			});
			list.appendChild(li);
		});
		ownedSection.appendChild(list);
	}
	container.appendChild(ownedSection);

	manualAddBtn.addEventListener('click', async () => {
		const name = manualInput.value.trim();
		if (!name) { manualInput.focus(); return; }
		setButtonLoading(manualAddBtn, 'Adding\u2026');
		try {
			await saveExpansion({ name }, game.id);
			if (onRefresh) await onRefresh();
			await renderExpansionsTab(game, { onRefresh });
		} catch (err) {
			showInlineError(manualAddBtn, err.message);
		} finally {
			clearButtonLoading(manualAddBtn);
		}
	});
}
