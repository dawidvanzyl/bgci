import { expect, test } from '../../fixtures/base';
import { catanBggId, isAddOf, isRemoveOf } from '../../fixtures/bggSeeds';
import {
	catanRefusal,
	releaseAndWaitForRemove,
	seedHeldRemove,
	waitUntilNoAddJob,
} from '../../fixtures/removeNotFinished';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Remove not finished — user adds a game whose BGG remove is not finished', { tag: ['@86IT10'] }, () => {
	test('S1 — a held remove refuses the add, and no add follows the release', async ({
		page,
		request,
		mockBgg,
		jobsDashboard,
	}) => {
		const collection = new CollectionPage(page);
		const collId = await seedHeldRemove(collection, request, mockBgg);

		await collection.importFromBgg('catan', 'Catan');
		const warning = collection.modalWarning;
		await expect(warning).toBeVisible();

		await expect(warning).toHaveText(catanRefusal);
		await expect(collection.modalTitle).toHaveText('Add from BGG');
		await expect(collection.modalTitle).toBeVisible();
		const warningBox = await collection.box(warning);
		const cancelBox = await collection.box(collection.cancelButton);
		const saveBox = await collection.box(collection.saveButton);
		expect(warningBox.bottom).toBeLessThanOrEqual(cancelBox.top);
		expect(warningBox.bottom).toBeLessThanOrEqual(saveBox.top);
		expect(await warning.evaluate((element) => getComputedStyle(element).marginTop)).toBe('16px');
		await expect(warning.getByRole('button')).toHaveCount(0);
		await expect(collection.saveButton).toHaveText('Save');
		await expect(collection.saveButton).toBeEnabled();
		await expect(collection.formField('name')).toHaveValue('Catan');

		await collection.cancelButton.click();
		await collection.reload();
		await expect(collection.card('Catan')).toHaveCount(0);

		await releaseAndWaitForRemove(mockBgg, collId);
		await waitUntilNoAddJob(jobsDashboard);
		const adds = await mockBgg.attempts('add', isAddOf(catanBggId));
		expect(adds).toHaveLength(1);
		const removes = await mockBgg.attempts('remove', isRemoveOf(collId));
		expect(adds[0].startedAt).toBeLessThan(removes[0].startedAt);
	});

	test('S2 — the warning leaves on its own after 5 seconds', async ({ page, request, mockBgg }) => {
		const collection = new CollectionPage(page);
		await seedHeldRemove(collection, request, mockBgg);

		await collection.importFromBgg('catan', 'Catan');
		await expect(collection.modalWarning).toBeVisible();
		const shownAt = Date.now();

		await expect.poll(() => Date.now() - shownAt, { intervals: [250], timeout: 4000 }).toBeGreaterThanOrEqual(3000);
		await expect(collection.modalWarning).toBeVisible();
		await expect(collection.modalWarning).toBeHidden({ timeout: 7000 - (Date.now() - shownAt) });
		expect(Date.now() - shownAt).toBeLessThanOrEqual(7000);
		await expect(collection.modalTitle).toHaveText('Add from BGG');
		await expect(collection.formField('name')).toBeVisible();
	});

	test('S3 — control: once the remove is finished, the same add is accepted', async ({ page, request, mockBgg }) => {
		const collection = new CollectionPage(page);
		const collId = await seedHeldRemove(collection, request, mockBgg);
		await releaseAndWaitForRemove(mockBgg, collId);

		await collection.importFromBgg('catan', 'Catan');

		await expect(page.locator('#modal-overlay')).toHaveClass(/hidden/);
		await expect(collection.modalWarning).toBeHidden();
		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.bggStatusMark('Catan')).toBeVisible();
	});
});
