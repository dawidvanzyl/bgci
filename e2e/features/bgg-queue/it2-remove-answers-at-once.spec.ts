import { expect, test } from '../../fixtures/base';
import {
	acceptedAddCollId,
	acceptedWriteLabels,
	catanBggId,
	isRemoveOf,
	jobTimeoutMs,
	seedSentGame,
	waitUntilWriteHeld,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Queue BGG writes — removing a game does not wait for BGG', { tag: ['@86IT2'] }, () => {
	test('S1 — the remove answers at once and reaches BGG after the release', async ({ page, request, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		const collId = await acceptedAddCollId(mockBgg, catanBggId);
		await mockBgg.holdWrites('all');

		await collection.remove('Catan');

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();
		await collection.expectNoWriteWarning();
		await waitUntilWriteHeld(mockBgg, 'remove', isRemoveOf(collId));
		expect((await mockBgg.acceptedWrites()).filter((write) => write.action === 'remove')).toEqual([]);

		await mockBgg.releaseWrites();

		await expect
			.poll(async () => (await acceptedWriteLabels(mockBgg)).at(-1), { timeout: jobTimeoutMs })
			.toBe(`remove:${collId}`);
	});

	test('S2 — the remove is kept after a reload', async ({ page, request, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		const collId = await acceptedAddCollId(mockBgg, catanBggId);
		await mockBgg.holdWrites('all');
		await collection.remove('Catan');
		await waitUntilWriteHeld(mockBgg, 'remove', isRemoveOf(collId));

		await collection.reload();

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();
		await mockBgg.releaseWrites();
	});
});
