import { expect, test } from '../../fixtures/base';
import { azulBggId, azulItem, seedFailedGame } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Sync BGG — a failed game that BGG now lists keeps the status Failed', { tag: ['@86IT27'] }, () => {
	test('S1 — a failed game that BGG now lists keeps Failed', async ({ page, request, mockBgg, jobsDashboard }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedFailedGame(collection, request, mockBgg, jobsDashboard, 'azul', 'Azul');
		await mockBgg.setCollection([azulItem]);

		await collection.sync();
		await expect(collection.card('Azul')).toHaveCount(1);
		await expect(collection.bggStatusMark('Azul')).toHaveAttribute('data-bgg-status', 'failed');

		await collection.reload();
		await expect(collection.card('Azul')).toHaveCount(1);
		await expect(collection.bggStatusMark('Azul')).toHaveAttribute('data-bgg-status', 'failed');

		expect(await mockBgg.attempts('add', (attempt) => attempt.bggId === azulBggId)).toHaveLength(3);
		expect(await mockBgg.attempts('remove')).toEqual([]);
		await jobsDashboard.openFailed();
		await expect(jobsDashboard.jobRows('BGG add')).toHaveCount(1);
	});
});
