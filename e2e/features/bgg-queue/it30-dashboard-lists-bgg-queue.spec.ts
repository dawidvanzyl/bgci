import { expect, test } from '../../fixtures/base';
import { addAzulFromBgg, addCatanFromBgg, catanBggId, isAddOf, waitUntilWriteHeld } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Queue BGG writes — the dashboard lists the add job on the bgg queue', { tag: ['@86IT30'] }, () => {
	test('S1 — the held add is listed and names the bgg queue', async ({ page, mockBgg, jobsDashboard }) => {
		await mockBgg.holdWrites('all');
		const collection = new CollectionPage(page);
		await collection.goto();
		await addCatanFromBgg(collection);
		await waitUntilWriteHeld(mockBgg, 'add', isAddOf(catanBggId));

		await jobsDashboard.openProcessing();
		await expect(jobsDashboard.jobRows('BGG add')).toHaveCount(1);
		await jobsDashboard.openJob('BGG add');

		await expect(jobsDashboard.stateCard('Enqueued')).toContainText('bgg');
		await mockBgg.releaseWrites();
	});

	test('S2 — a second add waits on the bgg queue', async ({ page, mockBgg, jobsDashboard }) => {
		await mockBgg.holdWrites('all');
		const collection = new CollectionPage(page);
		await collection.goto();
		await addCatanFromBgg(collection);
		await waitUntilWriteHeld(mockBgg, 'add', isAddOf(catanBggId));
		await addAzulFromBgg(collection);

		await jobsDashboard.openEnqueuedQueue('bgg');

		await expect(jobsDashboard.jobRows('BGG add')).toHaveCount(1);
		await mockBgg.releaseWrites();
	});
});
