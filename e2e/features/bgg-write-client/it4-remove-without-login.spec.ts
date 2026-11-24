import { expect, test } from '../../fixtures/base';
import { catanBggId, seedSentGame } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write client — remove when the jobs service cannot log in to BGG', { tag: ['@99IT4'] }, () => {
	test('S1 — remove with a rejected session, and no login possible', async ({
		page,
		request,
		mockBgg,
		jobsDashboard,
	}) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		const before = await mockBgg.report();
		await mockBgg.setLoginsRejected(true);

		await collection.remove('Catan');

		await expect(collection.card('Catan')).toHaveCount(0);
		await collection.expectNoWriteWarning();
		await jobsDashboard.waitUntilFailed('BGG remove');
		const after = await mockBgg.report();
		expect(after.writes.map((write) => `${write.action}:${write.bggId}`)).toEqual([`add:${catanBggId}`]);
		expect(after.rejectedWrites - before.rejectedWrites).toBe(1);
		expect(after.rejectedLogins - before.rejectedLogins).toBe(3);
	});

	test('S2 — the remove is kept after a reload', async ({ page, request, mockBgg, jobsDashboard }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		await mockBgg.setLoginsRejected(true);
		await collection.remove('Catan');
		await jobsDashboard.waitUntilFailed('BGG remove');

		await collection.reload();

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();
		const removes = (await mockBgg.acceptedWrites()).filter((write) => write.action === 'remove');
		expect(removes).toEqual([]);
	});
});
