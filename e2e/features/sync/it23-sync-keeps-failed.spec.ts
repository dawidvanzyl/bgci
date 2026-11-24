import { expect, test } from '../../fixtures/base';
import {
	catanItem,
	importBySync,
	openCatanExpansions,
	seedFailedGame,
	seedFailedKnownExpansion,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

const seafarers = 'Catan: Seafarers';

test.describe('Sync BGG — sync keeps games whose BGG add has failed', { tag: ['@86IT23'] }, () => {
	test('S1 — a failed game survives the sync', async ({ page, request, mockBgg, jobsDashboard }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedFailedGame(collection, request, mockBgg, jobsDashboard, 'azul', 'Azul');

		await collection.sync();

		await expect(collection.card('Azul')).toHaveCount(1);
		await expect(collection.bggStatusMark('Azul')).toHaveAttribute('data-bgg-status', 'failed');

		await collection.reload();
		await expect(collection.card('Azul')).toHaveCount(1);
		await expect(collection.bggStatusMark('Azul')).toHaveAttribute('data-bgg-status', 'failed');
	});

	test('S2 — a failed expansion survives the sync', async ({ page, request, mockBgg, jobsDashboard }) => {
		await mockBgg.setCollection([catanItem]);
		const collection = new CollectionPage(page);
		await collection.goto();
		await importBySync(collection, 'Catan');
		await openCatanExpansions(collection);
		await seedFailedKnownExpansion(collection, request, mockBgg, jobsDashboard, seafarers);
		await collection.closeModal();

		await collection.sync();
		await openCatanExpansions(collection);
		await expect(collection.ownedExpansionStatusMark(seafarers)).toHaveAttribute('data-bgg-status', 'failed');
		await collection.closeModal();

		await collection.reload();
		await openCatanExpansions(collection);
		await expect(collection.ownedExpansionStatusMark(seafarers)).toHaveAttribute('data-bgg-status', 'failed');
	});
});
