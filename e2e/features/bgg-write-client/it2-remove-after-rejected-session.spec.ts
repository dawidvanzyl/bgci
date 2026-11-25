import { expect, test } from '../../fixtures/base';
import { catanBggId, jobTimeoutMs, seedSentGame } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write client — remove after BGG rejects the held session', { tag: ['@99IT2'] }, () => {
	test('S1 — remove succeeds after the held session is rejected', async ({ page, request, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		const before = await mockBgg.report();
		const catanCollId = before.writes[0].collId;
		await mockBgg.expireSessions();

		await collection.remove('Catan');

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();
		await collection.expectNoWriteWarning();
		await expect
			.poll(async () => (await mockBgg.acceptedWrites()).map((write) => `${write.action}:${write.collId}`), {
				timeout: jobTimeoutMs,
			})
			.toEqual([`add:${catanCollId}`, `remove:${catanCollId}`]);
		const after = await mockBgg.report();
		expect(after.writes[0].bggId).toBe(catanBggId);
		expect(after.rejectedWrites - before.rejectedWrites).toBe(1);
		expect(after.acceptedLogins - before.acceptedLogins).toBe(1);
	});

	test('S2 — the remove is kept after a reload', async ({ page, request, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		await mockBgg.expireSessions();
		await collection.remove('Catan');
		await expect
			.poll(async () => (await mockBgg.acceptedWrites()).map((write) => write.action), { timeout: jobTimeoutMs })
			.toEqual(['add', 'remove']);

		await collection.reload();

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();
		await collection.expectNoWriteWarning();
	});
});
