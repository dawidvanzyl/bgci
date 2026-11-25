import { expect, test } from '../../fixtures/base';
import {
	acceptedAddCollId,
	acceptedWriteLabels,
	catanBggId,
	jobTimeoutMs,
	seedFailedRemove,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe(
	'Remove not finished — the owner retries a failed remove and the add is accepted',
	{ tag: ['@86IT14'] },
	() => {
		test('S1 — after the owner retries the failed remove, the add is accepted', async ({
			page,
			request,
			mockBgg,
			jobsDashboard,
		}) => {
			const collection = new CollectionPage(page);
			await seedFailedRemove(collection, request, mockBgg, jobsDashboard, 'catan', 'Catan');
			const collId = await acceptedAddCollId(mockBgg, catanBggId);

			await jobsDashboard.retryFailed('BGG remove');
			await expect
				.poll(async () => await acceptedWriteLabels(mockBgg), { timeout: jobTimeoutMs })
				.toContain(`remove:${collId}`);
			await jobsDashboard.waitUntilNotFailed('BGG remove');
			await mockBgg.holdWrites('next');
			await collection.importFromBgg('catan', 'Catan');

			await expect(page.locator('#modal-overlay')).toHaveClass(/hidden/);
			await expect(collection.modalWarning).toBeHidden();
			await expect(collection.card('Catan')).toBeVisible();
			await expect(collection.bggStatusMark('Catan')).toHaveAttribute('data-bgg-status', 'pending');

			await collection.reload();
			await expect(collection.card('Catan')).toBeVisible();
			await expect(collection.bggStatusMark('Catan')).toHaveAttribute('data-bgg-status', 'pending');
		});
	}
);
