import { expect, test } from '../../fixtures/base';
import {
	isAddOf,
	openCatanExpansions,
	seedFailedKnownExpansion,
	seedSentExpansion,
	seedSentGame,
	tradersAndBarbariansBggId,
	waitUntilWriteHeld,
} from '../../fixtures/bggSeeds';
import type { MockBgg } from '../../fixtures/mockBgg';
import type { JobsDashboardPage } from '../../pages/JobsDashboardPage';
import { CollectionPage } from '../../pages/CollectionPage';
import type { APIRequestContext } from '@playwright/test';

const sent = 'Catan: Seafarers';
const failed = 'Catan: Cities & Knights';
const pending = 'Catan: Traders & Barbarians';
const hand = 'House Rules Pack';

async function seedExpansions(
	collection: CollectionPage,
	request: APIRequestContext,
	mockBgg: MockBgg,
	dashboard: JobsDashboardPage
): Promise<void> {
	await collection.goto();
	await seedSentGame(collection, request, 'catan', 'Catan');
	await seedSentExpansion(collection, request, sent);
	await seedFailedKnownExpansion(collection, request, mockBgg, dashboard, failed);
	await collection.addManualExpansion(hand);
	await mockBgg.holdWrites('next');
	await collection.addKnownBggExpansion(pending);
	await waitUntilWriteHeld(mockBgg, 'add', isAddOf(tradersAndBarbariansBggId));
	await collection.reload();
	await openCatanExpansions(collection);
}

test.describe('Queue BGG writes — expansion rows show their BGG status', { tag: ['@86IT8'] }, () => {
	test('S1 — each row shows its own status', async ({ page, request, mockBgg, jobsDashboard }) => {
		test.setTimeout(120_000);
		const collection = new CollectionPage(page);

		await seedExpansions(collection, request, mockBgg, jobsDashboard);

		await expect(collection.ownedExpansionBadge(sent)).toBeVisible();
		await expect(collection.ownedExpansionStatusMark(sent)).toHaveCount(0);

		const failedMark = collection.ownedExpansionStatusMark(failed);
		await expect(failedMark).toHaveAttribute('data-bgg-status', 'failed');
		await expect(failedMark.locator('.bgg-status-glyph')).toHaveText('!');
		await expect(failedMark.locator('.bgg-status-text')).toHaveText('Failed');
		await expect(failedMark.locator('.bgg-status-text')).toBeVisible();

		const pendingMark = collection.ownedExpansionStatusMark(pending);
		await expect(pendingMark).toHaveAttribute('data-bgg-status', 'pending');
		await expect(pendingMark.locator('.bgg-status-glyph')).toHaveText('…');
		await expect(pendingMark.locator('.bgg-status-text')).toHaveText('Pending');
		await expect(pendingMark.locator('.bgg-status-text')).toBeVisible();

		await expect(collection.ownedExpansion(hand)).toBeVisible();
		await expect(collection.ownedExpansionBadge(hand)).toHaveCount(0);

		const row = collection.ownedExpansionRow(sent);
		const year = await collection.box(row.locator('.exp-year'));
		const badge = await collection.box(collection.ownedExpansionBadge(sent));
		const rating = await collection.box(row.locator('.badge-rating'));
		expect(badge.left).toBeGreaterThanOrEqual(year.right);
		expect(badge.right).toBeLessThanOrEqual(rating.left);
		await mockBgg.releaseWrites();
	});

	test('S2 — a click on the badge opens BGG and does not toggle the row', async ({
		page,
		context,
		request,
		mockBgg,
		jobsDashboard,
	}) => {
		test.setTimeout(120_000);
		await context.route('https://boardgamegeek.com/**', (route) =>
			route.fulfill({ status: 200, contentType: 'text/html', body: '<html><body>BGG</body></html>' })
		);
		const collection = new CollectionPage(page);
		await seedExpansions(collection, request, mockBgg, jobsDashboard);
		const popupOpened = context.waitForEvent('page');

		await collection.ownedExpansionBadge(sent).click();

		const popup = await popupOpened;
		await expect(popup).toHaveURL('https://boardgamegeek.com/boardgame/325');
		await expect(collection.ownedExpansionRow(sent).locator('.exp-owned-detail')).toBeHidden();
		await mockBgg.releaseWrites();
	});
});
