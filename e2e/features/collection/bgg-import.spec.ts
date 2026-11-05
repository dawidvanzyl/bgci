import { expect, test } from '../../fixtures/base';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Add game — user adds a game from a BGG search result', { tag: ['@61IT5'] }, () => {
	test('S1 — BGG details survive a reload', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();

		await collection.importFromBgg('catan', 'Catan');
		await expect(collection.card('Catan')).toBeVisible();
		await collection.reload();

		const card = collection.card('Catan');
		await expect(card.locator('.game-card-title')).toContainText('(1995)');
		await expect(card.locator('.badge-rating')).toHaveText(/7\.1/);
		await expect(card.locator('.badge-players')).toHaveText(/3–4/);
		await expect(card).toContainText('60–120 min');
		await expect(card.locator('.btn-edit')).toHaveCount(0);

		await collection.openInfo('Catan');
		const column = (header: string) =>
			collection.infoPanel.locator('.info-column', {
				has: page.locator('.info-column-header', { hasText: header }),
			});
		await expect(column('Categories').locator('.tag')).toHaveText(['Negotiation']);
		await expect(column('Mechanics').locator('.tag')).toHaveText(['Dice Rolling', 'Trading']);
		await expect(column('Credits').locator('.credits-name')).toHaveText(['Klaus Teuber', 'Michael Menzel', 'KOSMOS']);
		await expect(collection.infoPanel.locator('.tag-subdomain')).toHaveText(['Family Games']);
		await expect(collection.infoPanel.getByRole('link', { name: /View on BGG/ })).toHaveAttribute(
			'href',
			/boardgame\/13$/
		);
	});

	test('S2 — a BGG game and a manual game keep their source after reload', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.importFromBgg('catan', 'Catan');
		await expect(collection.card('Catan')).toBeVisible();
		await collection.addGame({ name: 'Homebrew Game' });

		await collection.reload();

		await expect(collection.card('Catan').locator('.btn-info')).toBeVisible();
		await expect(collection.card('Catan').locator('.btn-edit')).toHaveCount(0);
		await expect(collection.card('Homebrew Game').locator('.btn-edit')).toBeVisible();
		await expect(collection.card('Homebrew Game').locator('.btn-info')).toHaveCount(0);
	});
});
