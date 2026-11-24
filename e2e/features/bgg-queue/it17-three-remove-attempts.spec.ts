import { expect, test } from '../../fixtures/base';
import { acceptedAddCollId, catanBggId, seedSentGame } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Queue BGG writes — a failing remove makes three attempts and then fails', { tag: ['@86IT17'] }, () => {
	test('S1 — three remove attempts, then Failed in the dashboard', async ({
		page,
		request,
		mockBgg,
		jobsDashboard,
	}) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		const collId = await acceptedAddCollId(mockBgg, catanBggId);
		await mockBgg.failNextWrites(3);

		await collection.remove('Catan');
		await jobsDashboard.waitUntilFailed('BGG remove');

		await expect(jobsDashboard.jobRows('BGG remove')).toHaveCount(1);
		const removes = await mockBgg.attempts('remove');
		expect(removes).toHaveLength(3);
		for (const attempt of removes) {
			expect(attempt.collId).toBe(collId);
			expect(attempt.outcome).toBe('failed');
		}
		expect((await mockBgg.acceptedWrites()).filter((write) => write.action === 'remove')).toEqual([]);
		await expect(collection.card('Catan')).toHaveCount(0);
	});
});
