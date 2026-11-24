import { expect, test } from '../../fixtures/base';
import {
	acceptedAddCollId,
	acceptedWriteLabels,
	catanBggId,
	jobTimeoutMs,
	seedSentGame,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Queue BGG writes — a remove uses the id that the add stored', { tag: ['@86IT6'] }, () => {
	test('S1 — the remove uses the stored collection item id', async ({ page, request, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		const collId = await acceptedAddCollId(mockBgg, catanBggId);
		await mockBgg.setCollectionLookup(false);

		await collection.remove('Catan');

		await expect
			.poll(async () => (await acceptedWriteLabels(mockBgg)).at(-1), { timeout: jobTimeoutMs })
			.toBe(`remove:${collId}`);
	});
});
