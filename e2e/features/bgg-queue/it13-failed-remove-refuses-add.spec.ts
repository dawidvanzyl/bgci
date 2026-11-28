import { expect, test } from '../../fixtures/base';
import {
	acceptedAddCollId,
	catanBggId,
	isAddOf,
	isRemoveOf,
	jobTimeoutMs,
	seedFailedRemove,
	seedSentGame,
	waitUntilWriteHeld,
} from '../../fixtures/bggSeeds';
import { catanRefusal, waitUntilNoAddJob } from '../../fixtures/removeNotFinished';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Remove not finished — user adds a game whose BGG remove failed', { tag: ['@86IT13'] }, () => {
	test('S1 — a failed remove refuses the add', async ({ page, request, mockBgg, jobsDashboard }) => {
		const collection = new CollectionPage(page);
		await seedFailedRemove(collection, request, mockBgg, jobsDashboard, 'catan', 'Catan');

		await collection.importFromBgg('catan', 'Catan');
		await expect(collection.modalWarning).toBeVisible();

		await expect(collection.modalWarning).toHaveText(catanRefusal);
		await collection.cancelButton.click();
		await collection.reload();
		await expect(collection.card('Catan')).toHaveCount(0);
		await waitUntilNoAddJob(jobsDashboard);
		expect(await mockBgg.attempts('add', isAddOf(catanBggId))).toHaveLength(1);
	});

	test('S2 — boundary: a remove that is being retried also refuses the add', async ({ page, request, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		const collId = await acceptedAddCollId(mockBgg, catanBggId);
		await mockBgg.holdWrites('all');
		await mockBgg.failNextWrites(1);
		await collection.remove('Catan');
		await waitUntilWriteHeld(mockBgg, 'remove', isRemoveOf(collId));
		await mockBgg.releaseWrites({ keepHold: true });
		await expect
			.poll(
				async () =>
					(await mockBgg.attempts('remove', isRemoveOf(collId))).map(
						(attempt) => `${attempt.outcome}:${attempt.endedAt === null ? 'held' : 'ended'}`
					),
				{ message: 'The first remove failed and the retry is held', timeout: jobTimeoutMs }
			)
			.toEqual(['failed:ended', 'null:held']);

		await collection.importFromBgg('catan', 'Catan');
		await expect(collection.modalWarning).toBeVisible();

		await expect(collection.modalWarning).toHaveText(catanRefusal);
		await expect(collection.saveButton).toHaveText('Save');
		await collection.cancelButton.click();
		await collection.reload();
		await expect(collection.card('Catan')).toHaveCount(0);
		await mockBgg.releaseWrites();
	});
});
