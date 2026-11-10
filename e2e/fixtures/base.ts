import { test as base, type APIRequestContext } from '@playwright/test';
import { MockBgg } from './mockBgg';

const mockUrl = process.env.E2E_MOCK_URL ?? `http://localhost:${process.env.QA_MOCK_PORT ?? 15103}`;

async function removeAllGames(request: APIRequestContext): Promise<void> {
	const games = (await (await request.get('/api/games')).json()) as { id: string }[];
	for (const game of games) {
		await request.delete(`/api/games/${game.id}`);
	}
}

interface Fixtures {
	mockBgg: MockBgg;
	cleanState: void;
}

export const test = base.extend<Fixtures>({
	mockBgg: async ({}, use) => {
		await use(new MockBgg(mockUrl));
	},

	// Every test starts with an empty collection and a reset mock BGG.
	// Games go first, so the writer calls their removal makes are cleared by the reset.
	cleanState: [
		async ({ request, mockBgg }, use) => {
			await removeAllGames(request);
			await mockBgg.reset();
			await use();
		},
		{ auto: true },
	],
});

export { expect } from '@playwright/test';
