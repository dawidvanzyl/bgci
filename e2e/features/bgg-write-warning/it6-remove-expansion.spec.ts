import { expect, test } from '../../fixtures/base';
import {
	addSeafarersToCatan,
	catanBggId,
	expectInlineWarningPlacement,
	expectPageWarningHidden,
	seafarersBggId,
	seedCatanWithWriterCall,
	warningText,
} from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write warning — failed remove of an expansion', { tag: ['@72IT6'] }, () => {
	test('S1 — failed BGG remove of an expansion shows the inline warning', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedCatanWithWriterCall(collection, mockBgg);
		await addSeafarersToCatan(collection);
		await expect
			.poll(async () => (await mockBgg.writerCalls()).map((call) => `${call.action}:${call.bggId}`))
			.toEqual([`add:${catanBggId}`, `add:${seafarersBggId}`]);
		await mockBgg.setWriterFailing(true);

		await collection.removeExpansion('Catan: Seafarers');

		await expect(collection.ownedExpansion('Catan: Seafarers')).toHaveCount(0);
		await expect(collection.expansionsTab.getByRole('heading', { name: 'Owned Expansions (0)' })).toBeVisible();
		await expect(collection.expansionWarning.text).toHaveText(warningText.expansionRemove('Catan: Seafarers'));
		await expect(collection.expansionWarning.link).toBeVisible();
		await expect(collection.expansionWarning.dismissButton).toBeVisible();
		await expectInlineWarningPlacement(collection);
		await expectPageWarningHidden(collection);
	});
});
