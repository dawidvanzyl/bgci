import { expect, test } from '../../fixtures/base';
import { handGameName, seedStatusGames } from '../../fixtures/bggSeeds';
import { CollectionPage, type ViewMode } from '../../pages/CollectionPage';

const failedMarkColour = 'rgb(200, 168, 64)';

const views: { step: string; mode: ViewMode; wordVisible: boolean }[] = [
	{ step: 'S1 — large view', mode: 'Large icons', wordVisible: true },
	{ step: 'S2 — medium view', mode: 'Medium icons', wordVisible: false },
	{ step: 'S3 — small view', mode: 'Small icons', wordVisible: false },
	{ step: 'S4 — list view', mode: 'List', wordVisible: true },
	{ step: 'S5 — details view', mode: 'Details', wordVisible: true },
];

async function expectStatuses(collection: CollectionPage, wordVisible: boolean): Promise<void> {
	const pending = collection.bggStatusMark('Action Test');
	await expect(pending).toHaveAttribute('data-bgg-status', 'pending');
	await expect(pending.locator('.bgg-status-glyph')).toHaveText('…');
	const failed = collection.bggStatusMark('Azul');
	await expect(failed).toHaveAttribute('data-bgg-status', 'failed');
	await expect(failed.locator('.bgg-status-glyph')).toHaveText('!');
	if (wordVisible) {
		await expect(pending.locator('.bgg-status-text')).toBeVisible();
		await expect(pending.locator('.bgg-status-text')).toHaveText('Pending');
		await expect(failed.locator('.bgg-status-text')).toBeVisible();
		await expect(failed.locator('.bgg-status-text')).toHaveText('Failed');
	} else {
		await expect(pending.locator('.bgg-status-text')).toBeHidden();
		await expect(failed.locator('.bgg-status-text')).toBeHidden();
	}
	await expect(collection.bggBadge('Catan')).toBeVisible();
	await expect(collection.bggStatusMark('Catan')).toHaveCount(0);
	await expect(collection.card(handGameName)).toBeVisible();
	await expect(collection.bggBadge(handGameName)).toHaveCount(0);
}

test.describe('Queue BGG writes — the BGG status shows in every view mode', { tag: ['@86IT7'] }, () => {
	test('S1 to S5 — pending, failed, sent and hand-added games in each view', async ({
		page,
		request,
		mockBgg,
		jobsDashboard,
	}) => {
		test.setTimeout(120_000);
		const collection = new CollectionPage(page);
		await seedStatusGames(collection, request, mockBgg, jobsDashboard, { withHandGame: true });
		await expect(collection.cards).toHaveCount(4);

		for (const view of views) {
			await test.step(view.step, async () => {
				await collection.setView(view.mode);
				await expectStatuses(collection, view.wordVisible);
			});
		}

		await mockBgg.releaseWrites();
	});

	test('S6 — the failed mark is amber, not danger', async ({ page, request, mockBgg, jobsDashboard }) => {
		test.setTimeout(120_000);
		const collection = new CollectionPage(page);
		await seedStatusGames(collection, request, mockBgg, jobsDashboard, { withHandGame: true });
		await collection.setView('Large icons');

		const markColour = await collection.bggStatusMark('Azul').evaluate((element) => getComputedStyle(element).color);
		const removeButton = collection.card('Azul').locator('.btn-delete');
		const dangerText = await removeButton.evaluate((element) => getComputedStyle(element).color);
		const dangerFill = await removeButton.evaluate((element) => getComputedStyle(element).backgroundColor);

		expect(markColour).toBe(failedMarkColour);
		expect(markColour).not.toBe(dangerText);
		expect(markColour).not.toBe(dangerFill);
		await mockBgg.releaseWrites();
	});
});
