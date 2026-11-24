import { expect, test } from '../../fixtures/base';
import { catanBggId, jobTimeoutMs, seedFailedGame } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Queue BGG writes — the owner retries a failed add from the dashboard', { tag: ['@86IT26'] }, () => {
	test('S1 — retry from the dashboard clears the Failed mark', async ({ page, request, mockBgg, jobsDashboard }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedFailedGame(collection, request, mockBgg, jobsDashboard, 'catan', 'Catan');
		expect(await mockBgg.attempts('add')).toHaveLength(3);

		await jobsDashboard.openFailed();
		await jobsDashboard.openJob('BGG add');
		await jobsDashboard.requeue();
		await expect
			.poll(
				async () =>
					(await mockBgg.acceptedWrites()).some((write) => write.action === 'add' && write.bggId === catanBggId),
				{
					timeout: jobTimeoutMs,
				}
			)
			.toBe(true);
		await collection.reloadUntil(
			async () =>
				(await collection.bggStatusMark('Catan').count()) === 0 && (await collection.bggBadge('Catan').count()) === 1,
			'Catan shows its BGG badge with no status mark'
		);

		const adds = await mockBgg.attempts('add');
		expect(adds.map((attempt) => `${attempt.bggId}:${attempt.outcome}`)).toEqual([
			`${catanBggId}:failed`,
			`${catanBggId}:failed`,
			`${catanBggId}:failed`,
			`${catanBggId}:accepted`,
		]);
		await expect(collection.bggBadge('Catan')).toBeVisible();
		await expect(collection.bggBadge('Catan')).toHaveAccessibleName('BGG: In your BGG collection');
		await jobsDashboard.waitUntilNotFailed('BGG add');
	});
});
