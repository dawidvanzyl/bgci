import { expect, test } from '../../fixtures/base';
import { addCatanFromBgg, expectPageWarningPlacement, warningText } from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write warning — failed add of a game from a BGG search', { tag: ['@72IT3'] }, () => {
	test('S1 — failed BGG add of a game shows the page warning', async ({ page, mockBgg }) => {
		await mockBgg.setWriterFailing(true);
		const collection = new CollectionPage(page);
		await collection.goto();

		await addCatanFromBgg(collection);

		await expect(collection.pageWarning.root).toBeVisible();
		await expect(collection.pageWarning.text).toHaveText(warningText.gameAdd('Catan'));
		await expect(collection.pageWarning.link).toBeVisible();
		await expect(collection.pageWarning.dismissButton).toBeVisible();
		await expectPageWarningPlacement(collection);
		await expect(page.locator('#game-form')).toBeHidden();
	});

	test('S2 — the add is saved even though the BGG add failed', async ({ page, mockBgg }) => {
		await mockBgg.setWriterFailing(true);
		const collection = new CollectionPage(page);
		await collection.goto();
		await addCatanFromBgg(collection);

		await collection.reload();

		await expect(collection.card('Catan')).toBeVisible();
		expect(await mockBgg.writerCalls()).toEqual([]);
	});
});
