import { expect, test } from '../../fixtures/base';
import { catanBggId, seedCatanWithWriterCall, warningText } from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write client — remove when the API cannot log in to BGG', { tag: ['@99IT4'] }, () => {
	test('S1 — remove with a rejected session, and no login possible', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedCatanWithWriterCall(collection, mockBgg);
		const before = await mockBgg.report();
		await mockBgg.setLoginsRejected(true);

		await collection.remove('Catan');

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.pageWarning.root).toBeVisible();
		await expect(collection.pageWarning.text).toHaveText(warningText.gameRemove('Catan'));
		const after = await mockBgg.report();
		expect(after.writes.map((write) => `${write.action}:${write.bggId}`)).toEqual([`add:${catanBggId}`]);
		expect(after.rejectedLogins - before.rejectedLogins).toBe(1);
	});

	test('S2 — the remove is kept after a reload', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedCatanWithWriterCall(collection, mockBgg);
		await mockBgg.setLoginsRejected(true);
		await collection.remove('Catan');
		await expect(collection.pageWarning.text).toHaveText(warningText.gameRemove('Catan'));

		await collection.reload();

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();
		const removes = (await mockBgg.acceptedWrites()).filter((write) => write.action === 'remove');
		expect(removes).toEqual([]);
	});
});
