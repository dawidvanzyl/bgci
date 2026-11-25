import { expect, test } from '../../fixtures/base';
import {
	acceptedWriteLabels,
	addAzulFromBgg,
	addCatanFromBgg,
	azulBggId,
	catanBggId,
	isAddOf,
	jobTimeoutMs,
	waitUntilWriteHeld,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Queue BGG writes — one BGG write runs at a time across adds', { tag: ['@86IT20'] }, () => {
	test('S1 — the second add waits for the first', async ({ page, mockBgg }) => {
		await mockBgg.holdWrites('next');
		const collection = new CollectionPage(page);
		await collection.goto();

		await addCatanFromBgg(collection);
		await waitUntilWriteHeld(mockBgg, 'add', isAddOf(catanBggId));
		await addAzulFromBgg(collection);
		await expect(collection.bggStatusMark('Azul')).toHaveAttribute('data-bgg-status', 'pending');

		const whileHeld = (await mockBgg.report()).attempts;
		expect(whileHeld).toHaveLength(1);
		expect(whileHeld[0]).toMatchObject({ action: 'add', bggId: catanBggId, endedAt: null });
		expect(whileHeld.some((attempt) => attempt.bggId === azulBggId)).toBe(false);

		await mockBgg.releaseWrites();
		await expect
			.poll(async () => (await acceptedWriteLabels(mockBgg)).sort(), { timeout: jobTimeoutMs })
			.toEqual([`add:${azulBggId}`, `add:${catanBggId}`].sort());

		const report = await mockBgg.report();
		const catan = report.attempts.find((attempt) => attempt.bggId === catanBggId);
		const azul = report.attempts.find((attempt) => attempt.bggId === azulBggId);
		expect(azul!.startedAt).toBeGreaterThanOrEqual(catan!.endedAt!);
		expect(report.maxConcurrentWrites).toBe(1);
	});
});
