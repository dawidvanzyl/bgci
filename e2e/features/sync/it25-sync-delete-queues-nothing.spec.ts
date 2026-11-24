import { expect, test } from '../../fixtures/base';
import {
	acceptedAddCollId,
	addActionTestFromBgg,
	azulItem,
	catanBggId,
	catanItem,
	importBySync,
	isRemoveOf,
	jobTimeoutMs,
	seafarersItem,
	seedSentGame,
	waitForBggStatus,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';
import type { JobsDashboardPage } from '../../pages/JobsDashboardPage';
import type { APIRequestContext } from '@playwright/test';
import type { MockBgg } from '../../fixtures/mockBgg';

/**
 * A later write proves the BGG worker takes jobs. The dashboard then shows no BGG remove job waiting or running,
 * so every remove that the sync queued has ended and its attempt is in the mock BGG report.
 */
async function expectNoRemoveSent(
	collection: CollectionPage,
	request: APIRequestContext,
	mockBgg: MockBgg,
	jobsDashboard: JobsDashboardPage,
	collId: number
): Promise<void> {
	await addActionTestFromBgg(collection);
	await waitForBggStatus(request, 'Action Test', 'sent');
	await expect
		.poll(() => jobsDashboard.activeJobCount('bgg', 'BGG remove'), {
			message: 'No BGG remove job is enqueued or processing',
			timeout: jobTimeoutMs,
		})
		.toBe(0);

	expect(await mockBgg.attempts('remove')).toEqual([]);
	expect(await mockBgg.attempts('remove', isRemoveOf(collId))).toEqual([]);
}

test.describe('Sync BGG — a delete made by the sync queues no BGG remove', { tag: ['@86IT25'] }, () => {
	test('S1 — a game added from the BGG search and done leaves, with no remove', async ({
		page,
		request,
		mockBgg,
		jobsDashboard,
	}) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'catan', 'Catan');
		const collId = await acceptedAddCollId(mockBgg, catanBggId);

		await collection.sync();
		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();

		await collection.reload();
		await expect(collection.card('Catan')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();

		await expectNoRemoveSent(collection, request, mockBgg, jobsDashboard, collId);
	});

	test('S2 — a game imported by an earlier sync leaves when BGG stops listing it, with no remove', async ({
		page,
		request,
		mockBgg,
		jobsDashboard,
	}) => {
		await mockBgg.setCollection([azulItem]);
		const collection = new CollectionPage(page);
		await collection.goto();
		await importBySync(collection, 'Azul');
		await mockBgg.setCollection([]);

		await collection.sync();
		await expect(collection.card('Azul')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();

		await collection.reload();
		await expect(collection.card('Azul')).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();

		await expectNoRemoveSent(collection, request, mockBgg, jobsDashboard, azulItem.collId);
	});

	test('S3 — an expansion that BGG stops listing leaves, with no remove', async ({
		page,
		request,
		mockBgg,
		jobsDashboard,
	}) => {
		await mockBgg.setCollection([catanItem, seafarersItem]);
		const collection = new CollectionPage(page);
		await collection.goto();
		await importBySync(collection, 'Catan');
		await mockBgg.setCollection([catanItem]);

		await collection.sync();
		await collection.openInfo('Catan');
		await collection.openExpansionsTab();
		await expect(collection.ownedExpansion(seafarersItem.name)).toHaveCount(0);
		await collection.closeModal();

		await collection.reload();
		await collection.openInfo('Catan');
		await collection.openExpansionsTab();
		await expect(collection.ownedExpansion(seafarersItem.name)).toHaveCount(0);
		await collection.closeModal();
		await expect(collection.card('Catan')).toBeVisible();

		await expectNoRemoveSent(collection, request, mockBgg, jobsDashboard, seafarersItem.collId);
	});
});
