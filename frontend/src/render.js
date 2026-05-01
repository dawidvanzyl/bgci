import { allGames, currentViewMode, bggReachable } from './state.js';
import { esc } from './helpers.js';
import { sortGames } from './sort.js';

// ── DOM refs ───────────────────────────────────────────────
const gameGrid   = document.getElementById('game-grid');
const emptyState = document.getElementById('empty-state');

// ── Render ─────────────────────────────────────────────────
// onEdit(gameId)   — called when Edit button is clicked on a manually-added game
// onDelete(game)   — called when Remove button is clicked on any card
// onInfo(gameId)   — called when Info button is clicked on a BGG-sourced game
export function renderGames(filter = '', { onEdit, onDelete, onInfo } = {}) {
	// Exclude expansions (child games) from the main grid
	const topLevel = allGames.filter(g => !g.parentGameId);

	const filtered = filter
		? topLevel.filter(g =>
			g.name.toLowerCase().includes(filter) ||
			(g.categories || []).some(c => c.toLowerCase().includes(filter)) ||
			(g.mechanics  || []).some(m => m.toLowerCase().includes(filter))
		)
		: topLevel;

	const sorted = sortGames(filtered);

	// Remove existing cards but keep the empty state
	[...gameGrid.querySelectorAll('.game-card')].forEach(el => el.remove());

	if (sorted.length === 0) {
		emptyState.style.display = '';
		return;
	}
	emptyState.style.display = 'none';

	sorted.forEach(game => {
		gameGrid.appendChild(buildCard(game, { onEdit, onDelete, onInfo }));
	});
}

export function buildCard(game, { onEdit, onDelete, onInfo } = {}) {
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

	const expansionsBadge = game.expansionCount > 0
		? `<span class="badge badge-expansions">+${game.expansionCount} exp.</span>`
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
			<div class="game-card-meta">${bggBadge}${ratingBadge}${playersBadge}${timeBadge}${expansionsBadge}</div>
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
				<div class="game-card-meta">${bggBadge}${ratingBadge}${playersBadge}${timeBadge}${expansionsBadge}</div>
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
				<div class="game-card-meta">${bggBadge}${ratingBadgeSmall}${playersBadgeSmall}${expansionsBadge}</div>
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
				<div class="game-card-meta">${bggBadge}${ratingBadge}${playersBadge}${timeBadge}${expansionsBadge}</div>
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
				<div class="game-card-meta">${bggBadge}${ratingBadge}${playersBadge}${timeBadge}${expansionsBadge}</div>
				${tags ? `<div class="game-card-categories">${tags}</div>` : ''}
			</div>
			<div class="game-card-actions">
				${infoOrEdit}
				${deleteBtn}
			</div>
		`;
	}

	if (game.isBggSourced) {
		card.querySelector('.btn-info').addEventListener('click', () => onInfo && onInfo(game.id));
	} else {
		card.querySelector('.btn-edit').addEventListener('click', () => onEdit && onEdit(game.id));
	}
	card.querySelector('.btn-delete').addEventListener('click', () => onDelete && onDelete(game));

	return card;
}
