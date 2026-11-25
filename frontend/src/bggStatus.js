const SENTENCES = {
	sent: 'In your BGG collection',
	pending: 'Waiting to be added to your BGG collection',
	failed: 'Adding to your BGG collection failed',
};

const MARKS = {
	pending: { glyph: '…', word: 'Pending' },
	failed: { glyph: '!', word: 'Failed' },
};

export function bggStatusDisplay(game) {
	if (!game.isBggSourced) return null;
	const status = game.bggStatus in MARKS ? game.bggStatus : 'sent';
	const mark = MARKS[status];
	return {
		sentence: SENTENCES[status],
		mark: mark ? { ...mark, className: `bgg-status bgg-status--${status}`, status } : null,
	};
}

function markHtml(mark) {
	if (!mark) return '';
	return `<span class="${mark.className}" data-testid="bgg-status-mark" data-bgg-status="${mark.status}" aria-hidden="true"><span class="bgg-status-glyph">${mark.glyph}</span><span class="bgg-status-text">${mark.word}</span></span>`;
}

export function bggBadgeHtml(game, reachable) {
	const display = bggStatusDisplay(game);
	if (!display) return '';
	const inner = `<img src="/bgg-logo.svg" alt="BGG" class="bgg-badge-logo" />${markHtml(display.mark)}`;
	if (!reachable) {
		return `<span class="bgg-badge bgg-badge--disabled" data-testid="bgg-badge" title="BGG unavailable" aria-label="BGG unavailable">${inner}</span>`;
	}
	return `<a class="bgg-badge" data-testid="bgg-badge" href="https://boardgamegeek.com/boardgame/${Number(game.bggId)}" target="_blank" rel="noopener" aria-label="BGG: ${display.sentence}" title="${display.sentence}&#10;View on BoardGameGeek">${inner}</a>`;
}
