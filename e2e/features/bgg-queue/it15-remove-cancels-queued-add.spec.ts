import { expect, test } from '../../fixtures/base';
import {
	acceptedWriteLabels,
	addAzulFromBgg,
	addCatanFromBgg,
	azulBggId,
	catanBggId,
	isAddOf,
	jobTimeoutMs,
	waitUntilWriteHeld,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Cancel queued writes — removing a game whose add is still queued', { tag: ['@86IT15'] }, () => {
	test('S1 — removing a game whose add is still queued calls BGG for neither request', async ({
		page,
		mockBgg,
		jobsDashboard,
	}) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await mockBgg.holdWrites('next');
		await addAzulFromBgg(collection);
		await waitUntilWriteHeld(mockBgg, 'add', isAddOf(azulBggId));
		await addCatanFromBgg(collection);
		await expect(collection.bggStatusMark('Catan')).toHaveAttribute('data-bgg-status', 'pending');

		await collection.remove('Catan');
		await expect(collection.card('Catan')).toHaveCount(0);
		await mockBgg.releaseWrites();
		await jobsDashboard.waitUntilSucceeded('BGG add', 2);
		await expect
			.poll(
				async () =>
					(await jobsDashboard.activeJobCount('bgg', 'BGG add')) +
					(await jobsDashboard.activeJobCount('bgg', 'BGG remove')),
				{ message: 'No BGG add or remove job is enqueued or processing', timeout: jobTimeoutMs }
			)
			.toBe(0);

		expect(await mockBgg.attempts('add', isAddOf(catanBggId))).toEqual([]);
		expect(await mockBgg.attempts('remove')).toEqual([]);
		expect(await acceptedWriteLabels(mockBgg)).toContain(`add:${azulBggId}`);
		await collection.reload();
		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.bggBadge('Azul')).toBeVisible();
		await expect(collection.bggStatusMark('Azul')).toHaveCount(0);
	});
});
