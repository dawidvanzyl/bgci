import { expect, test } from '../../fixtures/base';
import {
	addCatanFromBgg,
	addSeafarersToCatan,
	expectPageWarningHidden,
	seedCatanWithWriterCall,
	warningText,
} from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write warning — the warning stays until the user dismisses it', { tag: ['@72IT8'] }, () => {
	test('S1 — the page warning stays through other actions until dismissed', async ({ page, mockBgg }) => {
		await mockBgg.setWriterFailing(true);
		const collection = new CollectionPage(page);
		await collection.goto();
		await addCatanFromBgg(collection);
		await expect(collection.pageWarning.root).toBeVisible();

		await collection.filter('Cat');
		await collection.clearFilter();
		await collection.openInfo('Catan');
		await collection.closeModal();
		await collection.addGame({ name: 'Lighthouse Keeper' });

		await expect(collection.pageWarning.root).toBeVisible();
		await expect(collection.pageWarning.text).toHaveText(warningText.gameAdd('Catan'));

		await collection.pageWarning.dismiss();

		await expectPageWarningHidden(collection);
		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.card('Lighthouse Keeper')).toBeVisible();
	});

	test('S2 — the inline expansion warning stays until dismissed', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedCatanWithWriterCall(collection, mockBgg);
		await mockBgg.setWriterFailing(true);
		await addSeafarersToCatan(collection);
		await expect(collection.expansionWarning.root).toBeVisible();

		await collection.addManualExpansion('Stormy Seas Pack');

		await expect(collection.expansionWarning.root).toBeVisible();
		await expect(collection.expansionWarning.text).toHaveText(warningText.expansionAdd('Catan: Seafarers'));

		await collection.expansionWarning.dismiss();

		await expect(collection.expansionWarning.root).toHaveCount(0);
		await expect(collection.ownedExpansion('Catan: Seafarers')).toBeVisible();
		await expect(collection.ownedExpansion('Stormy Seas Pack')).toBeVisible();
	});
});
