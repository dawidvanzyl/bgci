import { expect, test } from '../../fixtures/base';
import { seedFailedGame } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Queue BGG writes — the owner deletes a failed job in the dashboard', { tag: ['@86IT31'] }, () => {
	test('S1 — delete a failed job', async ({ page, request, mockBgg, jobsDashboard }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedFailedGame(collection, request, mockBgg, jobsDashboard, 'catan', 'Catan');
		await jobsDashboard.openFailed();
		await expect(jobsDashboard.jobRows('BGG add')).toHaveCount(1);

		await jobsDashboard.openJob('BGG add');
		await jobsDashboard.deleteJob();

		await jobsDashboard.waitUntilNotFailed('BGG add');
	});

	test('S2 — the game keeps its Failed mark', async ({ page, request, mockBgg, jobsDashboard }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedFailedGame(collection, request, mockBgg, jobsDashboard, 'catan', 'Catan');
		await jobsDashboard.openFailed();
		await jobsDashboard.openJob('BGG add');
		await jobsDashboard.deleteJob();
		await jobsDashboard.waitUntilNotFailed('BGG add');

		await collection.reload();

		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.bggStatusMark('Catan')).toHaveAttribute('data-bgg-status', 'failed');
		await expect(collection.bggStatusMark('Catan').locator('.bgg-status-text')).toHaveText('Failed');
	});
});
