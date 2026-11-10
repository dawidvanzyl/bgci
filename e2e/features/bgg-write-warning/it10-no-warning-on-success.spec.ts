import { expect, test } from '../../fixtures/base';
import {
	addCatanFromBgg,
	addSeafarersToCatan,
	catanBggId,
	expectPageWarningHidden,
	seafarersBggId,
} from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write warning — no warning when BGG writes succeed', { tag: ['@72IT10'] }, () => {
	test('S1 — successful add and remove of a game show no warning', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();

		await addCatanFromBgg(collection);

		await expect
			.poll(async () => (await mockBgg.writerCalls()).map((call) => `${call.action}:${call.bggId}`))
			.toEqual([`add:${catanBggId}`]);
		await expectPageWarningHidden(collection);
		const [added] = await mockBgg.writerCalls();

		await collection.remove('Catan');
		await expect(collection.card('Catan')).toHaveCount(0);

		await expect
			.poll(async () => (await mockBgg.writerCalls()).map((call) => `${call.action}:${call.collId}`))
			.toEqual([`add:${added.collId}`, `remove:${added.collId}`]);
		await expectPageWarningHidden(collection);
	});

	test('S2 — successful add and remove of an expansion show no inline warning', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await addCatanFromBgg(collection);

		await addSeafarersToCatan(collection);

		await expect(collection.expansionWarning.root).toHaveCount(0);
		await expectPageWarningHidden(collection);

		await collection.removeExpansion('Catan: Seafarers');
		await expect(collection.ownedExpansion('Catan: Seafarers')).toHaveCount(0);

		await expect(collection.expansionWarning.root).toHaveCount(0);
		await expectPageWarningHidden(collection);
		await expect
			.poll(async () => (await mockBgg.writerCalls()).map((call) => call.action))
			.toEqual(['add', 'add', 'remove']);
		const calls = await mockBgg.writerCalls();
		expect(calls[1].bggId).toBe(seafarersBggId);
	});
});
