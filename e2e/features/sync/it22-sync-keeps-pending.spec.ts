import { expect, test } from '../../fixtures/base';
import {
	addAzulFromBgg,
	addCatanFromBgg,
	catanBggId,
	catanItem,
	importBySync,
	isAddOf,
	openCatanExpansions,
	seafarersBggId,
	waitForBggStatus,
	waitUntilWriteHeld,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

const seafarers = 'Catan: Seafarers';

test.describe('Sync BGG — sync keeps games whose BGG add is pending', { tag: ['@86IT22'] }, () => {
	test('S1 — a pending game survives the sync, whether its add is at BGG or still queued', async ({
		page,
		request,
		mockBgg,
	}) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await mockBgg.holdWrites('next');
		await addCatanFromBgg(collection);
		await waitUntilWriteHeld(mockBgg, 'add', isAddOf(catanBggId));
		await addAzulFromBgg(collection);
		await waitForBggStatus(request, 'Azul', 'pending');

		await collection.sync();

		for (const name of ['Catan', 'Azul']) {
			await expect(collection.card(name)).toBeVisible();
			await expect(collection.bggStatusMark(name)).toHaveAttribute('data-bgg-status', 'pending');
		}

		await collection.reload();
		for (const name of ['Catan', 'Azul']) {
			await expect(collection.card(name)).toBeVisible();
			await expect(collection.bggStatusMark(name)).toHaveAttribute('data-bgg-status', 'pending');
		}

		await mockBgg.releaseWrites();
	});

	test('S2 — a pending expansion survives the sync', async ({ page, mockBgg }) => {
		await mockBgg.setCollection([catanItem]);
		const collection = new CollectionPage(page);
		await collection.goto();
		await importBySync(collection, 'Catan');
		await mockBgg.holdWrites('all');
		await openCatanExpansions(collection);
		await collection.addKnownBggExpansion(seafarers);
		await waitUntilWriteHeld(mockBgg, 'add', isAddOf(seafarersBggId));
		await collection.closeModal();

		await collection.sync();
		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.bggStatusMark('Catan')).toHaveCount(0);
		await openCatanExpansions(collection);
		await expect(collection.ownedExpansionStatusMark(seafarers)).toHaveAttribute('data-bgg-status', 'pending');
		await collection.closeModal();

		await collection.reload();
		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.bggStatusMark('Catan')).toHaveCount(0);
		await openCatanExpansions(collection);
		await expect(collection.ownedExpansionStatusMark(seafarers)).toHaveAttribute('data-bgg-status', 'pending');

		await mockBgg.releaseWrites();
	});
});
