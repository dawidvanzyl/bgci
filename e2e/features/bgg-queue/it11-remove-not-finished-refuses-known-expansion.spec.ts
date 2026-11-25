import { expect, test } from '../../fixtures/base';
import { catanSeafarersName, openCatanExpansions, seafarersBggId } from '../../fixtures/bggSeeds';
import { seafarersRefusal, seedHeldExpansionRemove } from '../../fixtures/removeNotFinished';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe(
	'Remove not finished — user adds a Known BGG expansion whose BGG remove is not finished',
	{ tag: ['@86IT11'] },
	() => {
		test('S1 — refused from Known BGG expansions', async ({ page, request, mockBgg }) => {
			const collection = new CollectionPage(page);
			await seedHeldExpansionRemove(collection, request, mockBgg);
			await expect(collection.ownedExpansion(catanSeafarersName)).toHaveCount(0);
			await expect(collection.knownExpansionOptions.filter({ hasText: catanSeafarersName })).toHaveCount(1);

			await collection.tryAddKnownBggExpansion(catanSeafarersName);
			const warning = collection.knownExpansionWarning;
			await expect(warning).toBeVisible();

			await expect(warning).toHaveText(seafarersRefusal);
			const labelBox = await collection.box(collection.knownExpansionLabel);
			const warningBox = await collection.box(warning);
			const rowBox = await collection.box(collection.knownExpansionRow);
			expect(labelBox.bottom).toBeLessThanOrEqual(warningBox.top);
			expect(warningBox.bottom).toBeLessThanOrEqual(rowBox.top);
			await expect(collection.knownAddButton).toHaveText('Add');
			await expect(collection.ownedExpansion(catanSeafarersName)).toHaveCount(0);

			await collection.closeModal();
			await collection.reload();
			await openCatanExpansions(collection);
			await expect(collection.ownedExpansion(catanSeafarersName)).toHaveCount(0);
			expect(await mockBgg.attempts('add', (attempt) => attempt.bggId === seafarersBggId)).toHaveLength(1);
		});
	}
);
