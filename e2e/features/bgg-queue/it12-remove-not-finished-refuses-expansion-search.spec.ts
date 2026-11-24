import type { Page } from '@playwright/test';
import { expect, test } from '../../fixtures/base';
import { catanSeafarersName, openCatanExpansions, seedSentGame } from '../../fixtures/bggSeeds';
import { seafarersRefusal, seedHeldExpansionRemove } from '../../fixtures/removeNotFinished';
import { CollectionPage } from '../../pages/CollectionPage';

/** Counts every browser dialog that opens on the page, and dismisses it. */
function countDialogs(page: Page): () => number {
	let count = 0;
	page.on('dialog', (dialog) => {
		count++;
		void dialog.dismiss();
	});
	return () => count;
}

test.describe(
	'Remove not finished — user adds an expansion from the Expansions tab search',
	{ tag: ['@86IT12'] },
	() => {
		test('S1 — refused from the Expansions tab search, without an alert', async ({ page, request, mockBgg }) => {
			const collection = new CollectionPage(page);
			await seedHeldExpansionRemove(collection, request, mockBgg);
			await mockBgg.setSearchIncludesExpansions(true);
			const dialogs = countDialogs(page);

			await collection.pickExpansionSearchResult('Seafarers', catanSeafarersName);
			const warning = collection.expansionSearchWarning;
			await expect(warning).toBeVisible();

			await expect(warning).toHaveText(seafarersRefusal);
			const labelBox = await collection.box(collection.expansionSearchLabel);
			const warningBox = await collection.box(warning);
			const rowBox = await collection.box(collection.expansionSearchRow);
			expect(warningBox.bottom).toBeLessThanOrEqual(rowBox.top);
			expect(warningBox.top).toBeGreaterThanOrEqual(labelBox.bottom);
			expect(dialogs()).toBe(0);
			await expect(collection.expansionSearchInput).toBeEnabled();
			await expect(collection.expansionSearchInput).toHaveValue(`${catanSeafarersName} (1997)`);
			await expect(collection.ownedExpansion(catanSeafarersName)).toHaveCount(0);

			await collection.closeModal();
			await collection.reload();
			await openCatanExpansions(collection);
			await expect(collection.ownedExpansion(catanSeafarersName)).toHaveCount(0);
		});

		test('S2 — another error on the search path is also inline, not an alert', async ({ page, request, mockBgg }) => {
			const collection = new CollectionPage(page);
			await collection.goto();
			await seedSentGame(collection, request, 'catan', 'Catan');
			await mockBgg.setSearchIncludesExpansions(true);
			await openCatanExpansions(collection);
			await collection.typeExpansionSearch('Seafarers', catanSeafarersName);
			await mockBgg.setAvailable(false);
			const dialogs = countDialogs(page);

			await collection.expansionSearchResultRow(catanSeafarersName).click();

			const warning = collection.expansionSearchWarning;
			await expect(warning).toBeVisible();
			await expect(warning).toHaveText('Failed to fetch BGG data for expansion.');
			const warningBox = await collection.box(warning);
			const rowBox = await collection.box(collection.expansionSearchRow);
			expect(warningBox.bottom).toBeLessThanOrEqual(rowBox.top);
			expect(dialogs()).toBe(0);
			await expect(collection.expansionSearchInput).toBeEnabled();
		});
	}
);
