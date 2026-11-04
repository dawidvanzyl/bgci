import { expect, test } from '../../fixtures/base';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Sync BGG — sync adds the games BGG lists', { tag: ['@61IT8'] }, () => {
	test('S1 — sync adds the listed base games', async ({ page, mockBgg }) => {
		await mockBgg.setCollection([
			{ bggId: 13, collId: 5101, name: 'Catan', year: 1995 },
			{ bggId: 230802, collId: 5102, name: 'Azul', year: 2017 },
		]);
		const collection = new CollectionPage(page);
		await collection.goto();

		await collection.sync();

		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.card('Azul')).toBeVisible();
		await collection.reload();
		await expect(collection.card('Catan').locator('.btn-info')).toBeVisible();
		await expect(collection.card('Azul').locator('.btn-info')).toBeVisible();
		await collection.openInfo('Catan');
		await expect(collection.infoPanel.locator('.info-column', { hasText: 'Mechanics' }).locator('.tag')).toHaveText([
			'Dice Rolling',
			'Trading',
		]);
	});

	test('S2 — sync adds a listed expansion under its parent', async ({ page, mockBgg }) => {
		await mockBgg.setCollection([
			{ bggId: 13, collId: 5201, name: 'Catan', year: 1995 },
			{ bggId: 325, collId: 5202, name: 'Catan: Seafarers', year: 1997, expansion: true },
		]);
		const collection = new CollectionPage(page);
		await collection.goto();

		await collection.sync();
		await collection.reload();

		await expect(collection.expansionBadge('Catan')).toHaveText('+1 expansion');
		await expect(collection.card('Catan: Seafarers')).toHaveCount(0);
		await collection.openInfo('Catan');
		await collection.openExpansionsTab();
		await expect(collection.ownedExpansion('Catan: Seafarers')).toBeVisible();
	});
});

test.describe('Sync BGG — sync removes BGG games it no longer lists', { tag: ['@61IT9'] }, () => {
	test('S1 — a synced game is removed when BGG stops listing it', async ({ page, mockBgg }) => {
		await mockBgg.setCollection([{ bggId: 230802, collId: 5301, name: 'Azul', year: 2017 }]);
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.sync();
		await expect(collection.card('Azul')).toBeVisible();
		await mockBgg.setCollection([]);

		await collection.sync();
		await collection.reload();

		await expect(collection.card('Azul')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();
	});

	test('S2 — only the unlisted BGG game is removed', async ({ page, mockBgg }) => {
		await mockBgg.setCollection([
			{ bggId: 13, collId: 5401, name: 'Catan', year: 1995 },
			{ bggId: 230802, collId: 5402, name: 'Azul', year: 2017 },
		]);
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.sync();
		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.card('Azul')).toBeVisible();
		await mockBgg.setCollection([{ bggId: 13, collId: 5401, name: 'Catan', year: 1995 }]);

		await collection.sync();
		await collection.reload();

		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.card('Azul')).toHaveCount(0);
	});
});

test.describe('Sync BGG — sync leaves manually added games alone', { tag: ['@61IT10'] }, () => {
	test('S1 — a manual game survives a sync of an empty BGG collection', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Shelf Copy Game', categories: 'Heirloom' });

		await collection.sync();
		await collection.reload();

		await expect(collection.card('Shelf Copy Game')).toBeVisible();
		await collection.openEdit('Shelf Copy Game');
		await expect(collection.formField('categories')).toHaveValue('Heirloom');
	});

	test('S2 — a manual game survives a sync that adds and removes BGG games', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Shelf Copy Game' });
		await collection.importFromBgg('catan', 'Catan');
		await expect(collection.card('Catan')).toBeVisible();
		await mockBgg.setCollection([{ bggId: 230802, collId: 5501, name: 'Azul', year: 2017 }]);

		await collection.sync();
		await collection.reload();

		await expect(collection.card('Shelf Copy Game')).toBeVisible();
		await expect(collection.card('Azul')).toBeVisible();
		await expect(collection.card('Catan')).toHaveCount(0);
	});
});
