import { expect, test } from '../../fixtures/base';
import { addCatanFromBgg, catanBggId, isAddOf, waitUntilWriteHeld } from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Queue BGG writes — adding a game from BGG does not wait for BGG', { tag: ['@86IT1'] }, () => {
	test('S1 — the add answers while BGG holds the write', async ({ page, mockBgg }) => {
		await mockBgg.holdWrites('all');
		const collection = new CollectionPage(page);
		await collection.goto();

		await addCatanFromBgg(collection);
		await waitUntilWriteHeld(mockBgg, 'add', isAddOf(catanBggId));

		await expect(page.locator('#modal-overlay')).toHaveClass(/hidden/);
		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.bggBadge('Catan')).toBeVisible();
		const mark = collection.bggStatusMark('Catan');
		await expect(mark).toHaveAttribute('data-bgg-status', 'pending');
		await expect(mark.locator('.bgg-status-glyph')).toHaveText('…');
		await expect(mark.locator('.bgg-status-text')).toHaveText('Pending');
		await expect(mark.locator('.bgg-status-text')).toBeVisible();
		await collection.expectNoWriteWarning();
		await mockBgg.releaseWrites();
	});

	test('S2 — the Pending game is kept after a reload', async ({ page, mockBgg }) => {
		await mockBgg.holdWrites('all');
		const collection = new CollectionPage(page);
		await collection.goto();
		await addCatanFromBgg(collection);
		await waitUntilWriteHeld(mockBgg, 'add', isAddOf(catanBggId));

		await collection.reload();

		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.bggStatusMark('Catan')).toHaveAttribute('data-bgg-status', 'pending');
		await expect(collection.bggStatusMark('Catan').locator('.bgg-status-text')).toHaveText('Pending');
		await mockBgg.releaseWrites();
	});
});
