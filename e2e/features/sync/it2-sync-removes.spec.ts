import { expect, test } from '../../fixtures/base';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Sync BGG — sync removes games that BGG no longer lists', { tag: ['@55IT2'] }, () => {
	test('S1 — sync removes a game that was imported by a BGG search', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.importFromBgg('catan', 'Catan');
		await expect(collection.card('Catan')).toBeVisible();

		await collection.clickSyncBgg();

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();

		await page.reload();
		await expect(collection.emptyState).toBeVisible();
		await expect(collection.card('Catan')).toHaveCount(0);
	});

	test('S2 — sync removes a game it imported earlier, after BGG stops listing it', async ({ page, mockBgg }) => {
		await mockBgg.setCollection([{ bggId: 230802, collId: 5001, name: 'Azul', year: 2017 }]);
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.clickSyncBgg();
		await expect(collection.card('Azul')).toBeVisible();
		await expect(collection.syncButton).toBeEnabled();
		await expect(collection.syncButton).toHaveText('↻ Sync BGG');
		await mockBgg.setCollection([]);

		await collection.clickSyncBgg();

		await expect(collection.card('Azul')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();

		await page.reload();
		await expect(collection.emptyState).toBeVisible();
		await expect(collection.card('Azul')).toHaveCount(0);
	});

	test('S3 — sync keeps a manually added game when it removes the BGG game', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addManually('Shelf Copy Game');
		await expect(collection.card('Shelf Copy Game')).toBeVisible();
		await collection.importFromBgg('catan', 'Catan');
		await expect(collection.card('Catan')).toBeVisible();

		await collection.clickSyncBgg();

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.card('Shelf Copy Game')).toBeVisible();

		await page.reload();
		await expect(collection.card('Shelf Copy Game')).toBeVisible();
		await expect(collection.card('Catan')).toHaveCount(0);
	});
});
