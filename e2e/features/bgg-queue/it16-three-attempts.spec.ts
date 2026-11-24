import { bggGame } from '../../fixtures/bggGames';
import { expect, test } from '../../fixtures/base';
import { addCatanFromBgg, catanBggId, seedFailedGame } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Queue BGG writes — a failing add makes three attempts and then fails', { tag: ['@86IT16'] }, () => {
	test('S1 — three attempts, then Failed', async ({ page, mockBgg, jobsDashboard }) => {
		const catan = bggGame(catanBggId);
		const collection = new CollectionPage(page);
		await mockBgg.failNextWrites(3);
		await collection.goto();

		await addCatanFromBgg(collection);
		await collection.reloadUntil(
			async () => (await collection.bggStatusMark('Catan').getAttribute('data-bgg-status')) === 'failed',
			'Catan shows the Failed mark'
		);
		await jobsDashboard.waitUntilFailed('BGG add');

		await expect(collection.bggStatusMark('Catan').locator('.bgg-status-glyph')).toHaveText('!');
		await expect(collection.bggStatusMark('Catan').locator('.bgg-status-text')).toHaveText('Failed');
		expect(await jobsDashboard.jobRows('BGG add').count()).toBeGreaterThan(0);
		const adds = await mockBgg.attempts('add');
		expect(adds).toHaveLength(3);
		for (const attempt of adds) {
			expect(attempt.bggId).toBe(catanBggId);
			expect(attempt.outcome).toBe('failed');
		}
		expect(await mockBgg.acceptedWrites()).toEqual([]);
		const card = collection.card('Catan');
		await expect(card).toContainText(String(catan.year));
		await expect(card).toContainText(catan.rating.toFixed(1));
		await expect(card.locator('.badge-players')).toContainText(`${catan.minPlayers}–${catan.maxPlayers}`);
		await expect(card.locator('.badge', { hasText: `${catan.minPlayTime}–${catan.maxPlayTime} min` })).toBeVisible();
	});

	test('S2 — the Failed game keeps its details in Info', async ({ page, request, mockBgg, jobsDashboard }) => {
		const catan = bggGame(catanBggId);
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedFailedGame(collection, request, mockBgg, jobsDashboard, 'catan', 'Catan');

		await collection.openInfo('Catan');

		await expect(collection.infoPanel).toContainText(catan.description);
		for (const name of [...catan.designers, ...catan.publishers]) {
			await expect(collection.infoPanel).toContainText(name);
		}
	});
});
