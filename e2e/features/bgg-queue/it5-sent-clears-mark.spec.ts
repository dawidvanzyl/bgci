import { expect, test } from '../../fixtures/base';
import { acceptedWriteLabels, addCatanFromBgg, catanBggId, jobTimeoutMs } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Queue BGG writes — a successful add clears the Pending mark', { tag: ['@86IT5'] }, () => {
	test('S1 — the mark clears after the job succeeds', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();

		await addCatanFromBgg(collection);
		await expect
			.poll(async () => (await acceptedWriteLabels(mockBgg)).includes(`add:${catanBggId}`), { timeout: jobTimeoutMs })
			.toBe(true);
		await collection.reloadUntil(
			async () =>
				(await collection.bggStatusMark('Catan').count()) === 0 && (await collection.bggBadge('Catan').count()) === 1,
			'Catan shows its BGG badge with no status mark'
		);

		await expect(collection.bggBadge('Catan')).toBeVisible();
		await expect(collection.bggStatusMark('Catan')).toHaveCount(0);
		await expect(collection.bggBadge('Catan')).toHaveAccessibleName('BGG: In your BGG collection');
	});
});
