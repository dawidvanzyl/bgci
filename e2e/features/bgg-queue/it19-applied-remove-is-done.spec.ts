import { expect, test } from '../../fixtures/base';
import { acceptedAddCollId, catanBggId, isRemoveOf, seedSentGame } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Check before writing — a remove that BGG applied is done, not failed', { tag: ['@86IT19'] }, () => {
	test('S1 — an applied remove whose answer was lost is done, not failed', async ({
		page,
		request,
		mockBgg,
		jobsDashboard,
	}) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		const collId = await acceptedAddCollId(mockBgg, catanBggId);
		await mockBgg.applyButFailNextWrites(1);

		await collection.remove('Catan');
		await jobsDashboard.waitUntilSucceeded('BGG remove');
		await jobsDashboard.openJob('BGG remove');

		const removes = await mockBgg.attempts('remove', isRemoveOf(collId));
		expect(removes.map((attempt) => attempt.outcome)).toEqual(['failed']);
		const accepted = (await mockBgg.acceptedWrites()).filter((write) => write.action === 'remove');
		expect(accepted).toHaveLength(1);
		await expect(jobsDashboard.stateCard('Failed')).toHaveCount(1);
		expect((await jobsDashboard.stateTitles())[0]).toBe('Succeeded');
		await jobsDashboard.openFailed();
		await expect(jobsDashboard.jobRows('BGG remove')).toHaveCount(0);
	});
});
