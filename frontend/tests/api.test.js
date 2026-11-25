import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

let syncBgg, saveGameFromBgg, saveExpansionFromBgg, deleteGame, BggAddRefusedError;

beforeAll(async () => {
	vi.stubGlobal('window', {});
	({ syncBgg, saveGameFromBgg, saveExpansionFromBgg, deleteGame } = await import('../src/api.js'));
	({ BggAddRefusedError } = await import('../src/bggRefusal.js'));
	vi.unstubAllGlobals();
});

afterEach(() => {
	vi.unstubAllGlobals();
});

describe('syncBgg', () => {
	it('sends POST to the sync endpoint and resolves', { tags: ['55UC1'] }, async () => {
		const fetchStub = vi.fn(async () => ({ ok: true, status: 200, text: async () => '' }));
		vi.stubGlobal('fetch', fetchStub);

		await expect(syncBgg()).resolves.toBeUndefined();

		expect(fetchStub).toHaveBeenCalledTimes(1);
		expect(fetchStub).toHaveBeenCalledWith('/api/sync/bgg', { method: 'POST' });
	});

	it('rejects with the response text when the server answers with an error', { tags: ['55UC2'] }, async () => {
		const body = 'BGG sync failed: upstream';
		vi.stubGlobal(
			'fetch',
			vi.fn(async () => ({ ok: false, status: 500, text: async () => body }))
		);

		const error = await syncBgg().catch((err) => err);

		expect(error).toBeInstanceOf(Error);
		expect(error.message).toBe(body);
	});
});

describe('saveGameFromBgg', () => {
	it('resolves with the id and the pending status and no write result', { tags: ['111UC19'] }, async () => {
		const body = { id: 'abc', bggStatus: 'pending' };
		vi.stubGlobal(
			'fetch',
			vi.fn(async () => ({ ok: true, status: 201, json: async () => body }))
		);

		const result = await saveGameFromBgg({ name: 'Wingspan', bggId: 266192 });

		expect(result).toEqual({ id: 'abc', bggStatus: 'pending' });
		expect(result).not.toHaveProperty('bggWrite');
	});
});

describe('deleteGame', () => {
	it('resolves with no value when the API answers no content', { tags: ['111UC19'] }, async () => {
		const fetchStub = vi.fn(async () => ({ ok: true, status: 204, text: async () => '' }));
		vi.stubGlobal('fetch', fetchStub);

		await expect(deleteGame('abc')).resolves.toBeUndefined();

		expect(fetchStub).toHaveBeenCalledWith('/api/games/abc', { method: 'DELETE' });
	});

	it('rejects with the response text when the API answers with an error', { tags: ['111UC19'] }, async () => {
		vi.stubGlobal(
			'fetch',
			vi.fn(async () => ({ ok: false, status: 404, text: async () => 'not found' }))
		);

		const error = await deleteGame('abc').catch((err) => err);

		expect(error.message).toBe('not found');
	});
});

describe('adding from BGG while a remove is not finished', () => {
	const refusal = () => ({ ok: false, status: 409, text: async () => '{"reason":"bgg-remove-not-finished"}' });

	it('throws a refusal carrying the game name for a base game', { tags: ['112UC7'] }, async () => {
		vi.stubGlobal(
			'fetch',
			vi.fn(async () => refusal())
		);

		const error = await saveGameFromBgg({ name: 'Wingspan', bggId: 266192 }).catch((err) => err);

		expect(error).toBeInstanceOf(BggAddRefusedError);
		expect(error.gameName).toBe('Wingspan');
	});

	it('throws a refusal carrying the preview name for an expansion', { tags: ['112UC7'] }, async () => {
		const answers = [
			{ ok: true, status: 200, json: async () => ({ name: 'Wingspan: Oceania Expansion', bggId: 300 }) },
			refusal(),
		];
		vi.stubGlobal(
			'fetch',
			vi.fn(async () => answers.shift())
		);

		const error = await saveExpansionFromBgg(300, 'parent').catch((err) => err);

		expect(error).toBeInstanceOf(BggAddRefusedError);
		expect(error.gameName).toBe('Wingspan: Oceania Expansion');
	});

	it('throws a plain error with the text for any other failure', { tags: ['112UC7'] }, async () => {
		const answers = [
			{ ok: false, status: 409, text: async () => '{"reason":"something-else"}' },
			{ ok: false, status: 500, text: async () => 'boom' },
			{ ok: false, status: 409, text: async () => 'not json' },
		];
		vi.stubGlobal(
			'fetch',
			vi.fn(async () => answers.shift())
		);

		const other = await saveGameFromBgg({ name: 'Wingspan' }).catch((err) => err);
		const server = await saveGameFromBgg({ name: 'Wingspan' }).catch((err) => err);
		const text = await saveGameFromBgg({ name: 'Wingspan' }).catch((err) => err);

		for (const [error, message] of [
			[other, '{"reason":"something-else"}'],
			[server, 'boom'],
			[text, 'not json'],
		]) {
			expect(error).not.toBeInstanceOf(BggAddRefusedError);
			expect(error.message).toBe(message);
		}
	});
});
