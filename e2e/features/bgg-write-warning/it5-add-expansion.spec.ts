import { expect, test } from '../../fixtures/base';
import {
	addSeafarersToCatan,
	expectInlineWarningPlacement,
	expectPageWarningHidden,
	seedCatanWithWriterCall,
	warningText,
} from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write warning — failed add of an expansion from BGG', { tag: ['@72IT5'] }, () => {
	test('S1 — failed BGG add of an expansion shows the inline warning', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedCatanWithWriterCall(collection, mockBgg);
		await mockBgg.setWriterFailing(true);

		await addSeafarersToCatan(collection);

		await expect(collection.ownedExpansion('Catan: Seafarers')).toBeVisible();
		await expect(collection.expansionWarning.root).toBeVisible();
		await expect(collection.expansionWarning.text).toHaveText(warningText.expansionAdd('Catan: Seafarers'));
		await expect(collection.expansionWarning.link).toBeVisible();
		await expect(collection.expansionWarning.dismissButton).toBeVisible();
		await expectInlineWarningPlacement(collection);
		await expectPageWarningHidden(collection);
	});

	test('S2 — the expansion is saved', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedCatanWithWriterCall(collection, mockBgg);
		await mockBgg.setWriterFailing(true);
		await addSeafarersToCatan(collection);
		await expect(collection.expansionWarning.root).toBeVisible();
		await collection.closeModal();

		await collection.reload();

		await expect(collection.expansionBadge('Catan')).toHaveText('+1 expansion');
		await collection.openInfo('Catan');
		await collection.openExpansionsTab();
		await expect(collection.ownedExpansion('Catan: Seafarers')).toBeVisible();
	});
});
