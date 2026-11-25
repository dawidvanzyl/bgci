import { expect, test } from '../../fixtures/base';
import {
	addKnownExpansionToCatan,
	isAddOf,
	seafarersBggId,
	seedSentGame,
	waitUntilWriteHeld,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

// The scenario "add from the BGG search inside the Expansions tab" is blocked. The API's search sends
// type=boardgame, so BGG answers no expansion for it, and the mock search is not widened.
test.describe('Queue BGG writes — adding an expansion does not wait for BGG', { tag: ['@86IT3'] }, () => {
	test('S1 — add from Known BGG expansions', async ({ page, request, mockBgg }) => {
		const dialogs: string[] = [];
		page.on('dialog', (dialog) => {
			dialogs.push(dialog.message());
			void dialog.dismiss();
		});
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		await mockBgg.holdWrites('all');

		await addKnownExpansionToCatan(collection, 'Catan: Seafarers');
		await waitUntilWriteHeld(mockBgg, 'add', isAddOf(seafarersBggId));

		await expect(collection.ownedExpansion('Catan: Seafarers')).toBeVisible();
		await expect(collection.ownedExpansionBadge('Catan: Seafarers')).toBeVisible();
		const mark = collection.ownedExpansionStatusMark('Catan: Seafarers');
		await expect(mark).toHaveAttribute('data-bgg-status', 'pending');
		await expect(mark.locator('.bgg-status-text')).toBeVisible();
		await expect(mark.locator('.bgg-status-text')).toHaveText('Pending');
		await expect(collection.expansionsTab.locator(':scope > *').first()).toContainText('Add Expansion');
		await expect(collection.infoPanel.getByRole('alert')).toHaveCount(0);
		await collection.expectNoWriteWarning();
		expect(dialogs).toEqual([]);
		await mockBgg.releaseWrites();
	});
});
