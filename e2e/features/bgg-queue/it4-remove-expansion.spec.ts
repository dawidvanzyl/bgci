import { expect, test } from '../../fixtures/base';
import {
	acceptedAddCollId,
	isRemoveOf,
	openCatanExpansions,
	seafarersBggId,
	seedSentExpansion,
	seedSentGame,
	waitUntilWriteHeld,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Queue BGG writes — removing an expansion does not wait for BGG', { tag: ['@86IT4'] }, () => {
	test('S1 — remove an expansion while BGG holds the write', async ({ page, request, mockBgg }) => {
		const dialogs: string[] = [];
		page.on('dialog', (dialog) => {
			dialogs.push(dialog.message());
			void dialog.dismiss();
		});
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		await seedSentExpansion(collection, request, 'Catan: Seafarers');
		const collId = await acceptedAddCollId(mockBgg, seafarersBggId);
		await collection.closeModal();
		await mockBgg.holdWrites('all');

		await openCatanExpansions(collection);
		await collection.removeExpansion('Catan: Seafarers');
		await waitUntilWriteHeld(mockBgg, 'remove', isRemoveOf(collId));

		await expect(collection.ownedExpansion('Catan: Seafarers')).toHaveCount(0);
		await expect(collection.expansionsTab.getByRole('heading', { name: 'Owned Expansions (0)' })).toBeVisible();
		await expect(collection.infoPanel.getByRole('alert')).toHaveCount(0);
		await collection.expectNoWriteWarning();
		expect(dialogs).toEqual([]);
		await collection.closeModal();
		await expect(collection.card('Catan')).toBeVisible();
		await mockBgg.releaseWrites();
	});
});
