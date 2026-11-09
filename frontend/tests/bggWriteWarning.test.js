import { describe, expect, it } from 'vitest';
import { bggWriteWarning } from '../src/bggWriteWarning.js';

describe('bggWriteWarning', () => {
	it('names the game and links to its BGG page when adding to BGG failed', { tags: ['90UC7'] }, () => {
		const warning = bggWriteWarning({
			bggWrite: 'failed',
			action: 'add',
			isExpansion: false,
			name: 'Wingspan',
			bggId: 266192,
		});

		expect(warning).toEqual({
			text: '"Wingspan" was added to your collection, but adding it to BGG failed. Add it to your BGG collection yourself.',
			url: 'https://boardgamegeek.com/boardgame/266192',
		});
	});

	it('names the expansion and links to its own BGG page when adding to BGG failed', { tags: ['90UC7'] }, () => {
		const warning = bggWriteWarning({
			bggWrite: 'failed',
			action: 'add',
			isExpansion: true,
			name: 'Wingspan: Oceania Expansion',
			bggId: 325,
		});

		expect(warning).toEqual({
			text: 'The expansion "Wingspan: Oceania Expansion" was added to your collection, but adding it to BGG failed. Add it to your BGG collection yourself.',
			url: 'https://boardgamegeek.com/boardgame/325',
		});
	});

	it('names the game when removing it from BGG failed', { tags: ['90UC8'] }, () => {
		const warning = bggWriteWarning({
			bggWrite: 'failed',
			action: 'remove',
			isExpansion: false,
			name: 'Wingspan',
			bggId: 266192,
		});

		expect(warning).toEqual({
			text: '"Wingspan" was removed from your collection, but removing it from BGG failed. Remove it from your BGG collection yourself.',
			url: 'https://boardgamegeek.com/boardgame/266192',
		});
	});

	it('names the expansion when removing it from BGG failed', { tags: ['90UC8'] }, () => {
		const warning = bggWriteWarning({
			bggWrite: 'failed',
			action: 'remove',
			isExpansion: true,
			name: 'Wingspan: Oceania Expansion',
			bggId: 325,
		});

		expect(warning).toEqual({
			text: 'The expansion "Wingspan: Oceania Expansion" was removed from your collection, but removing it from BGG failed. Remove it from your BGG collection yourself.',
			url: 'https://boardgamegeek.com/boardgame/325',
		});
	});

	it.each(['succeeded', 'skipped', 'none', undefined])(
		'returns no warning for the outcome %s',
		{ tags: ['90UC9'] },
		(bggWrite) => {
			const warning = bggWriteWarning({ bggWrite, action: 'add', isExpansion: false, name: 'Wingspan', bggId: 266192 });

			expect(warning).toBeNull();
		}
	);
});
