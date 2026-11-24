import { expect, test } from '../../fixtures/base';
import {
	acceptedWriteLabels,
	addCatanFromBgg,
	azulBggId,
	catanBggId,
	jobTimeoutMs,
	seedSentGame,
	waitForBggStatus,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write client — add after BGG rejects the held session', { tag: ['@99IT1'] }, () => {
	test('S1 — add succeeds after the held session is rejected', async ({ page, request, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'azul', 'Azul');
		const before = await mockBgg.report();
		await mockBgg.expireSessions();

		await addCatanFromBgg(collection);

		await expect
			.poll(() => acceptedWriteLabels(mockBgg), { timeout: jobTimeoutMs })
			.toEqual([`add:${azulBggId}`, `add:${catanBggId}`]);
		await waitForBggStatus(request, 'Catan', 'sent');
		const after = await mockBgg.report();
		expect(after.writes[1].username).toBe('qa-user');
		expect(after.rejectedWrites - before.rejectedWrites).toBe(1);
		expect(after.acceptedLogins - before.acceptedLogins).toBe(1);
		expect(
			(await mockBgg.attempts('add', (attempt) => attempt.bggId === catanBggId)).map((attempt) => attempt.outcome)
		).toEqual(['rejected', 'accepted']);
		await collection.expectNoWriteWarning();
	});

	test('S2 — the add is kept, and its collection item id works', async ({ page, request, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'azul', 'Azul');
		await mockBgg.expireSessions();
		await addCatanFromBgg(collection);
		await expect
			.poll(() => acceptedWriteLabels(mockBgg), { timeout: jobTimeoutMs })
			.toEqual([`add:${azulBggId}`, `add:${catanBggId}`]);
		await waitForBggStatus(request, 'Catan', 'sent');
		const afterAdd = await mockBgg.report();
		const catanCollId = afterAdd.writes[1].collId;

		await collection.reload();

		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.bggBadge('Catan')).toBeVisible();
		await expect(collection.bggStatusMark('Catan')).toHaveCount(0);
		await collection.expectNoWriteWarning();

		await collection.remove('Catan');

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect
			.poll(async () => (await mockBgg.acceptedWrites()).map((write) => `${write.action}:${write.collId}`), {
				timeout: jobTimeoutMs,
			})
			.toContain(`remove:${catanCollId}`);
		await collection.expectNoWriteWarning();
		expect((await mockBgg.report()).acceptedLogins).toBe(afterAdd.acceptedLogins);
	});
});
