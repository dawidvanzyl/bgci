import { expect, test } from '../../fixtures/base';
import { expectPageWarningPlacement, seedCatanWithWriterCall, warningText } from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write warning — failed remove of a game', { tag: ['@72IT4'] }, () => {
	test('S1 — failed BGG remove of a game shows the page warning', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedCatanWithWriterCall(collection, mockBgg);
		await mockBgg.setWriterFailing(true);

		await collection.remove('Catan');

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.pageWarning.root).toBeVisible();
		await expect(collection.pageWarning.text).toHaveText(warningText.gameRemove('Catan'));
		await expect(collection.pageWarning.link).toBeVisible();
		await expect(collection.pageWarning.dismissButton).toBeVisible();
		await expectPageWarningPlacement(collection);
		await expect(page.locator('#btn-confirm-delete')).toBeHidden();
	});

	test('S2 — the remove is kept after a reload', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedCatanWithWriterCall(collection, mockBgg);
		await mockBgg.setWriterFailing(true);
		await collection.remove('Catan');
		await expect(collection.card('Catan')).toHaveCount(0);

		await collection.reload();

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();
		const removeCalls = (await mockBgg.writerCalls()).filter((call) => call.action === 'remove');
		expect(removeCalls).toEqual([]);
	});
});
