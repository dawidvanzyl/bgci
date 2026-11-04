import { expect, test } from '../../fixtures/base';
import { CollectionPage } from '../../pages/CollectionPage';

async function addManualExpansionTo(collection: CollectionPage, parent: string, expansion: string): Promise<void> {
	await collection.openEdit(parent);
	await collection.openExpansionsTab();
	await collection.addManualExpansion(expansion);
	await collection.closeModal();
}

async function addSeafarersToImportedCatan(collection: CollectionPage): Promise<void> {
	await collection.importFromBgg('catan', 'Catan');
	await expect(collection.card('Catan')).toBeVisible();
	await collection.openInfo('Catan');
	await collection.openExpansionsTab();
	await collection.addKnownBggExpansion('Catan: Seafarers');
	await collection.closeModal();
}

test.describe('Expansions — user adds an owned expansion to a game', { tag: ['@61IT6'] }, () => {
	test('S1 — manual expansion on a manual game', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Lighthouse Keeper' });

		await addManualExpansionTo(collection, 'Lighthouse Keeper', 'Stormy Seas Pack');

		await expect(collection.expansionBadge('Lighthouse Keeper')).toHaveText('+1 expansion');
		await expect(collection.card('Stormy Seas Pack')).toHaveCount(0);
		await collection.openEdit('Lighthouse Keeper');
		await collection.openExpansionsTab();
		await expect(collection.ownedExpansion('Stormy Seas Pack')).toBeVisible();
	});

	test('S2 — known BGG expansion on a BGG game', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();

		await addSeafarersToImportedCatan(collection);

		await expect(collection.expansionBadge('Catan')).toHaveText('+1 expansion');
		await expect(collection.card('Catan: Seafarers')).toHaveCount(0);
		await collection.openInfo('Catan');
		await collection.openExpansionsTab();
		await expect(collection.ownedExpansion('Catan: Seafarers')).toBeVisible();
	});

	test('S3 — the expansion count is per parent', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Lighthouse Keeper' });
		await collection.addGame({ name: 'Orchard Party' });

		await addManualExpansionTo(collection, 'Lighthouse Keeper', 'Stormy Seas Pack');

		await expect(collection.expansionBadge('Lighthouse Keeper')).toHaveText('+1 expansion');
		await expect(collection.card('Orchard Party')).toBeVisible();
		await expect(collection.card('Orchard Party').locator('.badge-expansions')).toHaveCount(0);
	});
});

test.describe('Expansions — owned expansions persist after a reload', { tag: ['@61IT7'] }, () => {
	test('S1 — manual expansion after reload', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Lighthouse Keeper' });
		await addManualExpansionTo(collection, 'Lighthouse Keeper', 'Stormy Seas Pack');

		await collection.reload();

		await expect(collection.expansionBadge('Lighthouse Keeper')).toHaveText('+1 expansion');
		await expect(collection.card('Stormy Seas Pack')).toHaveCount(0);
		await collection.openEdit('Lighthouse Keeper');
		await collection.openExpansionsTab();
		await expect(collection.ownedExpansion('Stormy Seas Pack')).toBeVisible();
	});

	test('S2 — BGG expansion after reload', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await addSeafarersToImportedCatan(collection);

		await collection.reload();

		await expect(collection.expansionBadge('Catan')).toHaveText('+1 expansion');
		await expect(collection.card('Catan: Seafarers')).toHaveCount(0);
		await collection.openInfo('Catan');
		await collection.openExpansionsTab();
		await expect(collection.ownedExpansion('Catan: Seafarers')).toBeVisible();
	});
});
