import { describe, expect, it } from 'vitest';
import { bggBadgeHtml, bggStatusDisplay } from '../src/bggStatus.js';
import { createGame } from './factories/gameFactory.js';

describe('bggStatusDisplay', () => {
	it('gives no mark and the collection sentence for a sent game', { tags: ['111UC17'] }, () => {
		const display = bggStatusDisplay(createGame({ bggId: 13, bggStatus: 'sent' }));

		expect(display).toEqual({ sentence: 'In your BGG collection', mark: null });
	});

	it('gives the neutral pending mark with its glyph, word and class', { tags: ['111UC17'] }, () => {
		const display = bggStatusDisplay(createGame({ bggId: 13, bggStatus: 'pending' }));

		expect(display).toEqual({
			sentence: 'Waiting to be added to your BGG collection',
			mark: { glyph: '…', word: 'Pending', className: 'bgg-status bgg-status--pending', status: 'pending' },
		});
	});

	it('gives the failed mark with its glyph, word and class', { tags: ['111UC17'] }, () => {
		const display = bggStatusDisplay(createGame({ bggId: 13, bggStatus: 'failed' }));

		expect(display).toEqual({
			sentence: 'Adding to your BGG collection failed',
			mark: { glyph: '!', word: 'Failed', className: 'bgg-status bgg-status--failed', status: 'failed' },
		});
	});

	it('treats an unknown status of a BGG game as sent', { tags: ['111UC17'] }, () => {
		const display = bggStatusDisplay(createGame({ bggId: 13, bggStatus: 'unexpected' }));

		expect(display).toEqual({ sentence: 'In your BGG collection', mark: null });
	});

	it('gives nothing for a game added by hand', { tags: ['111UC18'] }, () => {
		expect(bggStatusDisplay(createGame())).toBeNull();
	});
});

describe('bggBadgeHtml', () => {
	it('puts the status sentence in the tooltip and the accessible name of the link', { tags: ['111UC17'] }, () => {
		const html = bggBadgeHtml(createGame({ bggId: 13, bggStatus: 'pending' }), true);

		expect(html).toContain('href="https://boardgamegeek.com/boardgame/13"');
		expect(html).toContain('aria-label="BGG: Waiting to be added to your BGG collection"');
		expect(html).toContain('title="Waiting to be added to your BGG collection&#10;View on BoardGameGeek"');
		expect(html).toContain('data-testid="bgg-badge"');
	});

	it('renders the failed mark with its glyph and word', { tags: ['111UC17'] }, () => {
		const html = bggBadgeHtml(createGame({ bggId: 13, bggStatus: 'failed' }), true);

		expect(html).toContain('class="bgg-status bgg-status--failed"');
		expect(html).toContain('data-testid="bgg-status-mark" data-bgg-status="failed"');
		expect(html).toContain('<span class="bgg-status-glyph">!</span><span class="bgg-status-text">Failed</span>');
	});

	it('renders no mark for a sent game', { tags: ['111UC17'] }, () => {
		const html = bggBadgeHtml(createGame({ bggId: 13, bggStatus: 'sent' }), true);

		expect(html).not.toContain('bgg-status');
		expect(html).toContain('aria-label="BGG: In your BGG collection"');
	});

	it('keeps the status mark on the disabled badge when BGG is unavailable', { tags: ['111UC17'] }, () => {
		const html = bggBadgeHtml(createGame({ bggId: 13, bggStatus: 'pending' }), false);

		expect(html).toContain('class="bgg-badge bgg-badge--disabled"');
		expect(html).toContain('title="BGG unavailable"');
		expect(html).toContain('aria-label="BGG unavailable"');
		expect(html).toContain('data-bgg-status="pending"');
		expect(html).not.toContain('href=');
	});

	it('gives an empty string for a game added by hand', { tags: ['111UC18'] }, () => {
		expect(bggBadgeHtml(createGame(), true)).toBe('');
		expect(bggBadgeHtml(createGame(), false)).toBe('');
	});
});
