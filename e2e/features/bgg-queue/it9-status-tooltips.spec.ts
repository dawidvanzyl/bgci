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

		await mockBgg.setAvailable(false);
		await collection.reloadUntil(
			async () => collection.statusBanner.isVisible(),
			'The BGG status banner shows while BGG is unavailable'
		);

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
