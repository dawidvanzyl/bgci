import { expect, test } from '../../fixtures/base';
import { addCatanFromBgg, catanBggId } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Check before writing — an add that BGG applied is not added again', { tag: ['@86IT18'] }, () => {
	test('S1 — an applied add whose answer was lost is not added twice', async ({ page, mockBgg, jobsDashboard }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await mockBgg.applyButFailNextWrites(1);

		await addCatanFromBgg(collection);
		await collection.reloadUntil(
			async () =>
				(await collection.bggBadge('Catan').count()) === 1 && (await collection.bggStatusMark('Catan').count()) === 0,
			'Catan shows its BGG badge with no status mark'
		);
		await jobsDashboard.waitUntilSucceeded('BGG add');
		await jobsDashboard.openJob('BGG add');

		const adds = await mockBgg.attempts('add', (attempt) => attempt.bggId === catanBggId);
		expect(adds.map((attempt) => attempt.outcome)).toEqual(['failed']);
		const accepted = (await mockBgg.acceptedWrites()).filter(
			(write) => write.action === 'add' && write.bggId === catanBggId
		);
		expect(accepted).toHaveLength(1);
		await expect(collection.bggStatusMark('Catan')).toHaveCount(0);
		await expect(jobsDashboard.stateCard('Failed')).toHaveCount(1);
		expect((await jobsDashboard.stateTitles())[0]).toBe('Succeeded');
	});

	test('S2 — control: an add that BGG did not apply is added on the next attempt', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await mockBgg.failNextWrites(1);

		await addCatanFromBgg(collection);
		await collection.reloadUntil(
			async () =>
				(await collection.bggBadge('Catan').count()) === 1 && (await collection.bggStatusMark('Catan').count()) === 0,
			'Catan shows its BGG badge with no status mark'
		);

		const adds = await mockBgg.attempts('add', (attempt) => attempt.bggId === catanBggId);
		expect(adds.map((attempt) => attempt.outcome)).toEqual(['failed', 'accepted']);
		const accepted = (await mockBgg.acceptedWrites()).filter(
			(write) => write.action === 'add' && write.bggId === catanBggId
		);
		expect(accepted).toHaveLength(1);
		await expect(collection.bggStatusMark('Catan')).toHaveCount(0);
	});
});
