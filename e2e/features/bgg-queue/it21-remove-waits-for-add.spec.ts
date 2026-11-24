import { expect, test } from '../../fixtures/base';
import {
	acceptedAddCollId,
	acceptedWriteLabels,
	addCatanFromBgg,
	azulBggId,
	catanBggId,
	isAddOf,
	jobTimeoutMs,
	seedSentGame,
	waitUntilWriteHeld,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Queue BGG writes — one BGG write runs at a time across adds and removes', { tag: ['@86IT21'] }, () => {
	test('S1 — a remove waits for an add', async ({ page, request, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'azul', 'Azul');
		const azulCollId = await acceptedAddCollId(mockBgg, azulBggId);
		await mockBgg.holdWrites('next');

		await addCatanFromBgg(collection);
		await waitUntilWriteHeld(mockBgg, 'add', isAddOf(catanBggId));
		await collection.remove('Azul');

		await expect(collection.card('Azul')).toHaveCount(0);
		expect(await mockBgg.attempts('remove')).toEqual([]);

		await mockBgg.releaseWrites();
		await expect
			.poll(async () => (await acceptedWriteLabels(mockBgg)).slice(1).sort(), { timeout: jobTimeoutMs })
			.toEqual([`add:${catanBggId}`, `remove:${azulCollId}`].sort());

		const report = await mockBgg.report();
		const add = report.attempts.find((attempt) => attempt.action === 'add' && attempt.bggId === catanBggId);
		const remove = report.attempts.find((attempt) => attempt.action === 'remove');
		expect(remove!.startedAt).toBeGreaterThanOrEqual(add!.endedAt!);
		expect(report.maxConcurrentWrites).toBe(1);
	});
});
