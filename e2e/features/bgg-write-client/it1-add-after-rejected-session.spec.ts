import { expect, test } from '../../fixtures/base';
import { azulBggId, catanBggId, expectPageWarningHidden, seedAzulWithWriterCall } from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write client — add after BGG rejects the held session', { tag: ['@99IT1'] }, () => {
	test('S1 — add succeeds after the held session is rejected', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedAzulWithWriterCall(collection, mockBgg);
		const before = await mockBgg.report();
		await mockBgg.expireSessions();

		await collection.importFromBgg('catan', 'Catan');

		await expect(collection.card('Catan')).toBeVisible();
		await expect
			.poll(async () => (await mockBgg.acceptedWrites()).map((write) => `${write.action}:${write.bggId}`))
			.toEqual([`add:${azulBggId}`, `add:${catanBggId}`]);
		const after = await mockBgg.report();
		expect(after.writes[1].username).toBe('qa-user');
		expect(after.rejectedWrites - before.rejectedWrites).toBe(1);
		expect(after.acceptedLogins - before.acceptedLogins).toBe(1);
		await expectPageWarningHidden(collection);
	});

	test('S2 — the add is kept, and its collection item id works', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedAzulWithWriterCall(collection, mockBgg);
		await mockBgg.expireSessions();
		await collection.importFromBgg('catan', 'Catan');
		await expect(collection.card('Catan')).toBeVisible();
		await expect
			.poll(async () => (await mockBgg.acceptedWrites()).map((write) => `${write.action}:${write.bggId}`))
			.toEqual([`add:${azulBggId}`, `add:${catanBggId}`]);
		const afterAdd = await mockBgg.report();
		const catanCollId = afterAdd.writes[1].collId;

		await collection.reload();

		await expect(collection.card('Catan')).toBeVisible();
		await expectPageWarningHidden(collection);

		await collection.remove('Catan');

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect
			.poll(async () => (await mockBgg.acceptedWrites()).map((write) => `${write.action}:${write.collId}`))
			.toContain(`remove:${catanCollId}`);
		await expectPageWarningHidden(collection);
		expect((await mockBgg.report()).acceptedLogins).toBe(afterAdd.acceptedLogins);
	});
});
