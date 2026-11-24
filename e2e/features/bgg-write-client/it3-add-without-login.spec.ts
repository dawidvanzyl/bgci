import { expect, test } from '../../fixtures/base';
import { restartJobs } from '../../fixtures/jobsRestart';
import {
	acceptedWriteLabels,
	addAzulFromBgg,
	addCatanFromBgg,
	azulBggId,
	jobTimeoutMs,
	seedSentGame,
	waitForBggStatus,
} from '../../fixtures/bggSeeds';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write client — add when the jobs service cannot log in to BGG', { tag: ['@99IT3'] }, () => {
	test('S1 — add with a held session that BGG rejects, and no login possible', async ({
		page,
		request,
		mockBgg,
		jobsDashboard,
	}) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'azul', 'Azul');
		const before = await mockBgg.report();
		await mockBgg.setLoginsRejected(true);

		await addCatanFromBgg(collection);

		await expect(collection.bggStatusMark('Catan')).toHaveAttribute('data-bgg-status', 'pending');
		await jobsDashboard.waitUntilFailed('BGG add');
		await waitForBggStatus(request, 'Catan', 'failed');
		await collection.reloadUntil(
			async () => (await collection.bggStatusMark('Catan').getAttribute('data-bgg-status')) === 'failed',
			'Catan shows the Failed mark'
		);
		const after = await mockBgg.report();
		expect(after.writes.map((write) => `${write.action}:${write.bggId}`)).toEqual([`add:${azulBggId}`]);
		expect(after.rejectedWrites - before.rejectedWrites).toBe(1);
		expect(after.rejectedLogins - before.rejectedLogins).toBe(3);
	});

	test('S2 — the add is kept after a reload', async ({ page, request, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedSentGame(collection, request, 'azul', 'Azul');
		await mockBgg.setLoginsRejected(true);
		await addCatanFromBgg(collection);

		await collection.reload();

		await expect(collection.card('Catan')).toBeVisible();
		expect(await acceptedWriteLabels(mockBgg)).toEqual([`add:${azulBggId}`]);
	});

	test('S3 — no session at all: nothing is sent to BGG but the login', async ({
		page,
		request,
		mockBgg,
		jobsDashboard,
	}) => {
		test.setTimeout(300_000);
		await restartJobs();
		await mockBgg.setLoginsRejected(true);
		const collection = new CollectionPage(page);
		await collection.goto();

		await addCatanFromBgg(collection);

		await jobsDashboard.waitUntilFailed('BGG add');
		await waitForBggStatus(request, 'Catan', 'failed');
		const report = await mockBgg.report();
		expect(report.writes).toEqual([]);
		expect(report.attempts).toEqual([]);
		expect(report.rejectedLogins).toBe(3);
		expect(report.rejectedWrites).toBe(0);
		await collection.reloadUntil(
			async () => (await collection.bggStatusMark('Catan').getAttribute('data-bgg-status')) === 'failed',
			'Catan shows the Failed mark'
		);
	});

	test('S4 — the jobs service recovers when logins work again', async ({ page, request, mockBgg, jobsDashboard }) => {
		await mockBgg.setLoginsRejected(true);
		const collection = new CollectionPage(page);
		await collection.goto();
		await addCatanFromBgg(collection);
		await jobsDashboard.waitUntilFailed('BGG add');
		await mockBgg.setLoginsRejected(false);

		await addAzulFromBgg(collection);

		await expect.poll(() => acceptedWriteLabels(mockBgg), { timeout: jobTimeoutMs }).toEqual([`add:${azulBggId}`]);
		await waitForBggStatus(request, 'Azul', 'sent');
	});
});
