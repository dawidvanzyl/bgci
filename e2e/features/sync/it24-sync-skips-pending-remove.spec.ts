import { expect, test } from '../../fixtures/base';
import {
	azulItem,
	catanItem,
	importBySync,
	isRemoveOf,
	jobTimeoutMs,
	openCatanExpansions,
	seafarersItem,
	waitUntilWriteHeld,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Sync BGG — sync does not import a game whose remove is pending', { tag: ['@86IT24'] }, () => {
	test('S1 — a game whose remove is at BGG (held) is not imported again', async ({ page, mockBgg }) => {
		await mockBgg.setCollection([azulItem]);
		const collection = new CollectionPage(page);
		await collection.goto();
		await importBySync(collection, 'Azul');
		await mockBgg.holdWrites('all');
		await collection.remove('Azul');
		await waitUntilWriteHeld(mockBgg, 'remove', isRemoveOf(azulItem.collId));

		await collection.sync();
		await expect(collection.card('Azul')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();

		await collection.reload();
		await expect(collection.card('Azul')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();

		await mockBgg.releaseWrites();
	});

	test('S2 — a game whose remove is being retried is not imported again', async ({ page, mockBgg }) => {
		await mockBgg.setCollection([azulItem]);
		const collection = new CollectionPage(page);
		await collection.goto();
		await importBySync(collection, 'Azul');
		await mockBgg.holdWrites('all');
		await mockBgg.failNextWrites(1);
		await collection.remove('Azul');
		await waitUntilWriteHeld(mockBgg, 'remove', isRemoveOf(azulItem.collId));
		await mockBgg.releaseWrites();
		await mockBgg.holdWrites('all');
		await expect
			.poll(
				async () =>
					(await mockBgg.attempts('remove', isRemoveOf(azulItem.collId))).map(
						(attempt) => `${attempt.outcome}:${attempt.endedAt === null ? 'held' : 'ended'}`
					),
				{ message: 'The first remove failed and the retry is held', timeout: jobTimeoutMs }
			)
			.toEqual(['failed:ended', 'null:held']);

		await collection.sync();
		await expect(collection.card('Azul')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();

		await collection.reload();
		await expect(collection.card('Azul')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();

		await mockBgg.releaseWrites();
	});

	test('S3 — an expansion whose remove is pending is not imported again', async ({ page, mockBgg }) => {
		await mockBgg.setCollection([catanItem, seafarersItem]);
		const collection = new CollectionPage(page);
		await collection.goto();
		await importBySync(collection, 'Catan');
		await mockBgg.holdWrites('all');
		await openCatanExpansions(collection);
		await collection.removeExpansion(seafarersItem.name);
		await waitUntilWriteHeld(mockBgg, 'remove', isRemoveOf(seafarersItem.collId));
		await collection.closeModal();

		await collection.sync();
		await openCatanExpansions(collection);
		await expect(collection.ownedExpansion(seafarersItem.name)).toHaveCount(0);
		await collection.closeModal();
		await expect(collection.card('Catan')).toBeVisible();

		await collection.reload();
		await openCatanExpansions(collection);
		await expect(collection.ownedExpansion(seafarersItem.name)).toHaveCount(0);
		await collection.closeModal();
		await expect(collection.card('Catan')).toBeVisible();

		await mockBgg.releaseWrites();
	});
});
