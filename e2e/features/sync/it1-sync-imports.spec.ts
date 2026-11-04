import { expect, test } from '../../fixtures/base';
import { CollectionPage } from '../../pages/CollectionPage';

const azul = { bggId: 230802, collId: 5001, name: 'Azul', year: 2017 };

test.describe('Sync BGG — sync imports games from the BGG collection', { tag: ['@55IT1'] }, () => {
	test('S1 — sync imports a game that is only on BGG', async ({ page, mockBgg }) => {
		await mockBgg.setCollection([azul]);
		const collection = new CollectionPage(page);
		await collection.goto();
		await expect(collection.emptyState).toBeVisible();
		await expect(collection.card('Azul')).toHaveCount(0);

		await collection.clickSyncBgg();

		await expect(collection.card('Azul')).toBeVisible();
		await expect(collection.emptyState).toBeHidden();
		await expect(collection.syncButton).toBeEnabled();
		await expect(collection.syncButton).toHaveText('↻ Sync BGG');

		await page.reload();
		await expect(collection.card('Azul')).toBeVisible();
	});

	test('S2 — a second sync does not duplicate a game already imported', async ({ page, mockBgg }) => {
		await mockBgg.setCollection([azul]);
		const collection = new CollectionPage(page);
		await collection.goto();

		await collection.clickSyncBgg();
		await expect(collection.card('Azul')).toHaveCount(1);
		await expect(collection.syncButton).toBeEnabled();
		await expect(collection.syncButton).toHaveText('↻ Sync BGG');

		await collection.clickSyncBgg();
		await expect(collection.syncButton).toBeEnabled();
		await expect(collection.syncButton).toHaveText('↻ Sync BGG');
		await expect(collection.card('Azul')).toHaveCount(1);

		await page.reload();
		await expect(collection.card('Azul')).toHaveCount(1);
	});
});
