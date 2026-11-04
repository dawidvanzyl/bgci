import { expect, test } from '../../fixtures/base';
import { CollectionPage } from '../../pages/CollectionPage';

// Baseline specs written before the epic and IT-code workflow existed, so they carry @smoke
// instead of an IT code. They prove the QA stack and the page objects work end to end.
test.describe('Collection smoke', { tag: '@smoke' }, () => {
	test('shows the empty state when the collection has no games', async ({ page }) => {
		const collection = new CollectionPage(page);

		await collection.goto();

		await expect(collection.emptyState).toBeVisible();
	});

	test('adds a game manually and removes it', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();

		await collection.addManually('Manual Test Game');
		await expect(collection.card('Manual Test Game')).toBeVisible();

		await collection.remove('Manual Test Game');
		await expect(collection.card('Manual Test Game')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();
	});

	test('imports a game from a BGG search and writes it back to BGG', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();

		await collection.importFromBgg('catan', 'Catan');

		await expect(collection.card('Catan')).toBeVisible();
		await expect
			.poll(async () => (await mockBgg.writerCalls()).filter((call) => call.action === 'add'))
			.toEqual([expect.objectContaining({ username: 'qa-user', bggId: 13 })]);
	});
});
