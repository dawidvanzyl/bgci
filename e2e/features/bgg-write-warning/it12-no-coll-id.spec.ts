import { expect, test } from '../../fixtures/base';
import { addCatanFromBgg, warningText } from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write warning — remove of a game that has no collection item id', { tag: ['@72IT12'] }, () => {
	test('S1 — removing a BGG game with no collection item id warns, even though the writer works', async ({
		page,
		mockBgg,
	}) => {
		await mockBgg.setWriterFailing(true);
		const collection = new CollectionPage(page);
		await collection.goto();
		await addCatanFromBgg(collection);
		await expect(collection.pageWarning.text).toHaveText(warningText.gameAdd('Catan'));
		await collection.pageWarning.dismiss();
		await mockBgg.setWriterFailing(false);
		await collection.reload();

		await collection.remove('Catan');

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.pageWarning.root).toBeVisible();
		await expect(collection.pageWarning.text).toHaveText(warningText.gameRemove('Catan'));
		expect(await mockBgg.writerCalls()).toEqual([]);
	});
});
