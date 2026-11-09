import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

let syncBgg, saveGameFromBgg, deleteGame;

beforeAll(async () => {
	vi.stubGlobal('window', {});
	({ syncBgg, saveGameFromBgg, deleteGame } = await import('../src/api.js'));
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
	it('resolves with the body when the BGG write failed', { tags: ['90UC7'] }, async () => {
		const body = { id: 'abc', bggWrite: 'failed' };
		vi.stubGlobal(
			'fetch',
			vi.fn(async () => ({ ok: true, status: 201, json: async () => body }))
		);

		await expect(saveGameFromBgg({ name: 'Wingspan', bggId: 266192 })).resolves.toEqual(body);
	});
});

describe('deleteGame', () => {
	it('resolves with the body when the BGG write failed', { tags: ['90UC8'] }, async () => {
		const body = { bggWrite: 'failed' };
		const fetchStub = vi.fn(async () => ({ ok: true, status: 200, json: async () => body }));
		vi.stubGlobal('fetch', fetchStub);

		await expect(deleteGame('abc')).resolves.toEqual(body);

		expect(fetchStub).toHaveBeenCalledWith('/api/games/abc', { method: 'DELETE' });
	});
});
