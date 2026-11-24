import { expect, test } from '../../fixtures/base';
import { seedStatusGames } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

const link = 'View on BoardGameGeek';

const sentences = {
	pending: 'Waiting to be added to your BGG collection',
	sent: 'In your BGG collection',
	failed: 'Adding to your BGG collection failed',
};

test.describe('Queue BGG writes — the BGG badge tooltip names the status', { tag: ['@86IT9'] }, () => {
	test('S1 — tooltip and accessible name per status', async ({ page, request, mockBgg, jobsDashboard }) => {
		test.setTimeout(120_000);
		const collection = new CollectionPage(page);
		await seedStatusGames(collection, request, mockBgg, jobsDashboard, { withHandGame: false });

		const expected = [
			{ name: 'Action Test', sentence: sentences.pending },
			{ name: 'Catan', sentence: sentences.sent },
			{ name: 'Azul', sentence: sentences.failed },
		];
		for (const { name, sentence } of expected) {
			const badge = collection.bggBadge(name);
			await expect(badge, name).toHaveAttribute('title', new RegExp(`^${sentence}\\s*\\n\\s*${link}$`));
			await expect(badge, name).toHaveAccessibleName(`BGG: ${sentence}`);
		}
		await mockBgg.releaseWrites();
	});

	test('S2 — BGG unavailable keeps today’s tooltip', async ({ page, request, mockBgg, jobsDashboard }) => {
		test.setTimeout(120_000);
		const collection = new CollectionPage(page);
		await seedStatusGames(collection, request, mockBgg, jobsDashboard, { withHandGame: false });

		// The app learns that BGG is down only from a slow background probe, so the page gets the
		// unavailable answer from /api/config, as the other specs do.
		await page.route('**/api/config', async (route) => {
			const response = await route.fetch();
			const config = (await response.json()) as Record<string, unknown>;
			await route.fulfill({
				response,
				json: { ...config, bggReachable: false, bggSearchEnabled: false, bggCollectionEnabled: false },
			});
		});
		await collection.reload();
		await expect(collection.statusBanner).toBeVisible();

		for (const name of ['Action Test', 'Catan', 'Azul']) {
			const badge = collection.bggBadge(name);
			await expect(badge, name).toHaveClass(/bgg-badge--disabled/);
			await expect(badge, name).toHaveAttribute('title', 'BGG unavailable');
		}
		await expect(collection.bggStatusMark('Action Test')).toHaveAttribute('data-bgg-status', 'pending');
		await expect(collection.bggStatusMark('Azul')).toHaveAttribute('data-bgg-status', 'failed');
		await mockBgg.releaseWrites();
	});
});
