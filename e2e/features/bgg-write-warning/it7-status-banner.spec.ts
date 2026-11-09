import { expect, test } from '../../fixtures/base';
import { seedCatanWithWriterCall, warningText } from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write warning — remove while BGG is unavailable', { tag: ['@72IT7'] }, () => {
	test('S1 — the game remove warning stacks directly under the status banner', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedCatanWithWriterCall(collection, mockBgg);
		await page.route('**/api/config', async (route) => {
			const response = await route.fetch();
			const config = (await response.json()) as Record<string, unknown>;
			await route.fulfill({
				response,
				json: { ...config, bggReachable: false, bggSearchEnabled: false, bggCollectionEnabled: false },
			});
		});
		await collection.reload();
		await expect(collection.statusBanner).toHaveText(
			'BGG is currently unavailable — your collection is loaded from cache'
		);
		await mockBgg.setWriterFailing(true);

		await collection.remove('Catan');

		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.pageWarning.text).toHaveText(warningText.gameRemove('Catan'));
		await expect(collection.pageWarning.link).toBeVisible();
		await expect(collection.statusBanner).toBeVisible();

		const banner = await collection.box(collection.statusBanner);
		const warning = await collection.box(collection.pageWarning.root);
		const toolbar = await collection.box(collection.toolbarBlock);
		expect(Math.abs(warning.top - banner.bottom)).toBeLessThanOrEqual(1);
		expect(warning.bottom).toBeLessThanOrEqual(toolbar.top + 1);
		await expect(collection.pageWarning.root.locator('xpath=preceding-sibling::*[1]')).toHaveId('bgg-status-banner');
	});
});
