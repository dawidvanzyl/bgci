import { allGames, currentViewMode, bggReachable } from './state.js';
import { esc } from './helpers.js';
import { sortGames } from './sort.js';

// ── DOM refs ───────────────────────────────────────────────
const gameGrid   = document.getElementById('game-grid');
const emptyState = document.getElementById('empty-state');

// ── Single-expand tracking for list view ──────────────────
let _expandedListCard   = null;
let _expandedListDetail = null;

// ── Badge helpers ──────────────────────────────────────────

const DIFFICULTY_LEVELS = [
	{ max: 1.5, label: 'Apprentice',  color: '#9e9e9e' },
	{ max: 2.5, label: 'Journeyman',  color: '#4caf50' },
	{ max: 3.5, label: 'Adept',       color: '#2196f3' },
	{ max: 4.5, label: 'Expert',      color: '#9c27b0' },
	{ max: 5.0, label: 'Grandmaster', color: '#ff9800' },
];

function difficultyBadge(bggWeight) {
	if (!bggWeight) return '';
	const level = DIFFICULTY_LEVELS.find(l => bggWeight <= l.max) ?? DIFFICULTY_LEVELS[DIFFICULTY_LEVELS.length - 1];
	return `<span class="badge badge-difficulty" style="--diff-color:${level.color}">${level.label}</span>`;
}

function ageBadge(minAge) {
	if (!minAge) return '';
	return `<span class="badge badge-age">${minAge}+</span>`;
}

function recommendedPlayersBadge(bestMin, bestMax) {
	if (!bestMin) return '';
	const range = !bestMax
		? `${bestMin}p`
		: bestMax >= 99
			? `${bestMin}+`
			: `${bestMin}\u2013${bestMax}p`;
	return `<span class="badge badge-best-players">&#9733; Best ${range}</span>`;
}

function playTimeBadge(minPlayTime, maxPlayTime, playTimeMinutes) {
	if (minPlayTime && maxPlayTime && minPlayTime !== maxPlayTime) {
		return `<span class="badge">&#9201; ${minPlayTime}\u2013${maxPlayTime} min</span>`;
	}
	const mins = maxPlayTime || minPlayTime || playTimeMinutes;
	if (!mins) return '';
	return `<span class="badge">&#9201; ${mins} min</span>`;
}

// Muted second line showing first subdomain · first publisher (+N)
// Used by Details card view and Large card view
function subdomainPublisherLine(subdomains, publishers) {
	const sub     = (subdomains  || [])[0];
	const pub     = (publishers  || [])[0];
	const pubRest = (publishers  || []).length - 1;
	const parts   = [];
	if (sub) parts.push(esc(sub));
	if (pub) parts.push(pubRest > 0
		? `${esc(pub)} <span class="publisher-more">+${pubRest}</span>`
		: esc(pub));
	if (!parts.length) return '';
	return `<div class="game-card-publisher">${parts.join(' <span class="sep-dot">&middot;</span> ')}</div>`;
}

// ── Render ─────────────────────────────────────────────────
export function renderGames(filter = '', { onEdit, onDelete, onInfo } = {}) {
	// Reset single-expand state on every render
	_expandedListCard   = null;
	_expandedListDetail = null;

	// Exclude expansions (child games) from the main grid
	const topLevel = allGames.filter(g => !g.parentGameId);

	const filtered = filter
		? topLevel.filter(g =>
			g.name.toLowerCase().includes(filter) ||
			(g.categories  || []).some(c => c.toLowerCase().includes(filter)) ||
			(g.mechanics   || []).some(m => m.toLowerCase().includes(filter)) ||
			(g.designers   || []).some(d => d.toLowerCase().includes(filter)) ||
			(g.artists     || []).some(a => a.toLowerCase().includes(filter)) ||
			(g.publishers  || []).some(p => p.toLowerCase().includes(filter)) ||
			(g.subdomains  || []).some(s => s.toLowerCase().includes(filter))
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

	const timeBadge = playTimeBadge(game.minPlayTimeMinutes, game.maxPlayTimeMinutes, game.playTimeMinutes);
	const diffBadge = difficultyBadge(game.bggWeight);
	const aBadge    = ageBadge(game.minAge);
	const bestPlayers = recommendedPlayersBadge(game.bestPlayerCountMin, game.bestPlayerCountMax);

	const expansionsBadge = game.expansionCount > 0
		? `<span class="badge badge-expansions">+${game.expansionCount} expansion${game.expansionCount === 1 ? '' : 's'}</span>`
		: '';

	const expansionsBadgeOverlay = game.expansionCount > 0
		? `<span class="badge badge-expansions badge-expansions--overlay">+${game.expansionCount} expansion${game.expansionCount === 1 ? '' : 's'}</span>`
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

	// ── List view ────────────────────────────────────────────
	if (currentViewMode === 'list') {
		card.className = 'game-card game-card--list';

		// Sub-line: subdomain tag pills · first publisher +N  (always reserve height)
		const subTags  = (game.subdomains  || []).map(s => `<span class="tag tag--subdomain tag--sm">${esc(s)}</span>`).join('');
		const pubFirst = (game.publishers || [])[0];
		const pubRest  = (game.publishers || []).length - 1;
		const pubText  = pubFirst
			? (pubRest > 0 ? `${esc(pubFirst)} <span class="publisher-more">+${pubRest}</span>` : esc(pubFirst))
			: '';
		const subLine  = (subTags || pubText)
			? `${subTags}${subTags && pubText ? '<span class="sep-dot">&middot;</span>' : ''}${pubText}`
			: '&nbsp;';

		// Action buttons (header area)
		const infoBtn   = `<button class="btn btn-secondary btn-info  list-action-btn">Info</button>`;
		const editBtn   = `<button class="btn btn-secondary btn-edit  list-action-btn">&#9998; Edit</button>`;
		const delBtn    = `<button class="btn btn-danger   btn-delete list-action-btn">Remove</button>`;
		const actionBtn = game.isBggSourced ? infoBtn : editBtn;

		// Expanded body: cover thumbnail + description + BGG link or Edit button
		const coverThumb = game.coverImageUrl
			? `<img class="list-detail-cover" src="${esc(game.coverImageUrl)}" alt="${esc(game.name)}" loading="lazy" />`
			: `<div class="list-detail-cover list-detail-cover--placeholder">&#127921;</div>`;

		const bggOrEditDetail = game.isBggSourced
			? (bggReachable
				? `<a class="btn btn-secondary btn-sm" href="https://boardgamegeek.com/boardgame/${game.bggId}" target="_blank" rel="noopener" style="text-decoration:none;align-self:flex-start">&#128279; View on BGG</a>`
				: '')
			: `<button class="btn btn-secondary btn-sm btn-edit-detail">&#9998; Edit</button>`;

		card.innerHTML = `
			<div class="list-row-header">
				<div class="list-row-left">
					<div class="list-row-title">${esc(game.name)}${game.year ? ` <span class="list-row-year">(${game.year})</span>` : ''}</div>
					<div class="list-row-sub">${subLine}</div>
				</div>
				<div class="list-row-sep">&nbsp;</div>
				<div class="list-row-badges">${bggBadge}${ratingBadge}${playersBadge}${timeBadge}${aBadge}${diffBadge}${bestPlayers}${expansionsBadge}</div>
				<div class="list-row-actions">
					${actionBtn}
					${delBtn}
				</div>
			</div>
			<div class="list-row-detail hidden">
				${coverThumb}
				<div class="list-detail-right">
					${game.description ? `<div class="list-detail-desc">${esc(game.description)}</div>` : ''}
					${bggOrEditDetail}
					${(game.subdomains || []).length ? `<div class="game-card-subdomains">${(game.subdomains || []).map(s => `<span class="tag tag--subdomain">${esc(s)}</span>`).join('')}</div>` : ''}
				</div>
			</div>
		`;

		// Wire Edit-in-detail button for manually-added games
		const editDetail = card.querySelector('.btn-edit-detail');
		if (editDetail) editDetail.addEventListener('click', () => onEdit && onEdit(game.id));

		// Single-expand: collapse previous before expanding new
		const header  = card.querySelector('.list-row-header');
		const detail  = card.querySelector('.list-row-detail');
		const actions = card.querySelector('.list-row-actions');
		header.addEventListener('click', e => {
			if (actions.contains(e.target)) return;
			const isExpanded = card.classList.contains('is-expanded');
			// Collapse the previously expanded card
			if (_expandedListCard && _expandedListCard !== card) {
				_expandedListCard.classList.remove('is-expanded');
				_expandedListDetail.classList.add('hidden');
			}
			// Toggle this card
			card.classList.toggle('is-expanded', !isExpanded);
			detail.classList.toggle('hidden', isExpanded);
			_expandedListCard   = !isExpanded ? card   : null;
			_expandedListDetail = !isExpanded ? detail : null;
		});

	// ── Details view ─────────────────────────────────────────
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
				<div class="game-card-title">${esc(game.name)}${game.year ? ` <span class="game-card-year">(${game.year})</span>` : ''}</div>
			${subdomainPublisherLine(game.subdomains, game.publishers)}
			<div class="game-card-meta">${bggBadge}${ratingBadge}${playersBadge}${timeBadge}${aBadge}${diffBadge}${bestPlayers}${expansionsBadge}</div>
			${(game.subdomains || []).length ? `<div class="game-card-subdomains">${(game.subdomains || []).map(s => `<span class="tag tag--subdomain">${esc(s)}</span>`).join('')}</div>` : ''}
				${game.description ? `<div class="game-description">${esc(game.description)}</div>` : ''}
				${allTags ? `<div class="game-card-categories">${allTags}</div>` : ''}
			</div>
			<div class="game-card-actions">
				${infoOrEdit}
				${deleteBtn}
			</div>
		`;

	// ── Small view ────────────────────────────────────────────
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
			<div class="game-card-cover-wrap">
				${coverHtml}
				${expansionsBadgeOverlay}
			</div>
			<div class="game-card-body">
				<div class="game-card-title">${esc(game.name)}</div>
				<div class="game-card-meta">${bggBadge}${ratingBadgeSmall}${playersBadgeSmall}</div>
			</div>
			<div class="game-card-actions">
				${infoOrEditSmall}
				${deleteBtnSmall}
			</div>
		`;

	// ── Medium view ───────────────────────────────────────────
	} else if (currentViewMode === 'medium') {
		const coverHtml = game.coverImageUrl
			? `<img class="game-card-cover" src="${esc(game.coverImageUrl)}" alt="${esc(game.name)}" loading="lazy" />`
			: `<div class="game-card-cover-placeholder">&#127921;</div>`;

		card.className = 'game-card game-card--medium';
		card.innerHTML = `
			<div class="game-card-cover-wrap">
				${coverHtml}
				${expansionsBadgeOverlay}
			</div>
			<div class="game-card-body">
				<div class="game-card-title">${esc(game.name)}${game.year ? ` <span class="game-card-year">(${game.year})</span>` : ''}</div>
				
				<div class="game-card-meta">${bggBadge}${ratingBadge}${playersBadge}${timeBadge}</div>
			</div>
			<div class="game-card-actions">
				${infoOrEdit}
				${deleteBtn}
			</div>
		`;

	// ── Large view (default) ──────────────────────────────────
	} else {
		const coverHtml = game.coverImageUrl
			? `<img class="game-card-cover" src="${esc(game.coverImageUrl)}" alt="${esc(game.name)}" loading="lazy" />`
			: `<div class="game-card-cover-placeholder">&#127921;</div>`;

		const tags = [...(game.categories || []).slice(0, 3), ...(game.mechanics || []).slice(0, 2)]
			.map(t => `<span class="tag">${esc(t)}</span>`).join('');

		const secondMeta = diffBadge || aBadge || bestPlayers
			? `<div class="game-card-meta game-card-meta--secondary">${diffBadge}${aBadge}${bestPlayers}</div>`
			: '';

		card.className = 'game-card game-card--large';
		card.innerHTML = `
			<div class="game-card-cover-wrap">
				${coverHtml}
				${expansionsBadgeOverlay}
			</div>
			<div class="game-card-body">
				<div class="game-card-title">${esc(game.name)}${game.year ? ` <span class="game-card-year">(${game.year})</span>` : ''}</div>
				
				<div class="game-card-meta">${bggBadge}${ratingBadge}${playersBadge}${timeBadge}</div>
				${secondMeta}
				<div class="game-card-categories">${tags}</div>
			</div>
			<div class="game-card-actions">
				${infoOrEdit}
				${deleteBtn}
			</div>
		`;
	}

	// ── Wire action buttons (all views) ───────────────────────
	if (game.isBggSourced) {
		card.querySelector('.btn-info').addEventListener('click', () => onInfo && onInfo(game.id));
	} else {
		card.querySelector('.btn-edit').addEventListener('click', () => onEdit && onEdit(game.id));
	}
	card.querySelector('.btn-delete').addEventListener('click', () => onDelete && onDelete(game));

	return card;
}
