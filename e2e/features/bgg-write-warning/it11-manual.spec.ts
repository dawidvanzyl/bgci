import { expect, test } from '../../fixtures/base';
import { expectPageWarningHidden } from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write warning — no warning for manually added items', { tag: ['@72IT11'] }, () => {
	test('S1 — removing a manual game shows no warning, even while the writer fails', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Lighthouse Keeper' });
		await mockBgg.setWriterFailing(true);

		await collection.remove('Lighthouse Keeper');

		await expect(collection.card('Lighthouse Keeper')).toHaveCount(0);
		await expectPageWarningHidden(collection);
		expect(await mockBgg.writerCalls()).toEqual([]);
	});

	test('S2 — removing a manual expansion shows no inline warning', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Lighthouse Keeper' });
		await collection.openEdit('Lighthouse Keeper');
		await collection.openExpansionsTab();
		await collection.addManualExpansion('Stormy Seas Pack');
		await mockBgg.setWriterFailing(true);

		await collection.removeExpansion('Stormy Seas Pack');

		await expect(collection.expansionsTab.getByRole('heading', { name: 'Owned Expansions (0)' })).toBeVisible();
		await expect(collection.expansionWarning.root).toHaveCount(0);
		await expectPageWarningHidden(collection);
		expect(await mockBgg.writerCalls()).toEqual([]);
	});
});
