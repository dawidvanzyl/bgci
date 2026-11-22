import { expect, test } from '../../fixtures/base';
import { JobsDashboardPage } from '../../pages/JobsDashboardPage';

test.describe('Jobs service — the owner opens the dashboard on the jobs port', { tag: ['@86IT28'] }, () => {
	test.beforeEach(async ({ page }) => {
		await new JobsDashboardPage(page).waitUntilAnswering();
	});

	test('S1 — the dashboard opens at the root of the jobs port', async ({ page }) => {
		const dashboard = new JobsDashboardPage(page);

		const status = await dashboard.goto();

		expect(status).toBe(200);
		await dashboard.expectDashboard();
	});

	test('S2 — Hangfire runs a server that serves the BGG queue with one worker', async ({ page }) => {
		const dashboard = new JobsDashboardPage(page);
		await dashboard.goto();

		await dashboard.openFromNavigation('Servers');

		const bggServer = dashboard.serverRows
			.filter({ hasText: 'bgg' })
			.filter({ has: page.getByRole('cell', { name: '1', exact: true }) });
		await expect(bggServer.first()).toBeVisible();
		const queues = await dashboard.serverQueues.allTextContents();
		expect(queues.length).toBeGreaterThan(0);
		for (const queue of queues) {
			expect(queue.trim()).toBe('bgg');
		}
	});

	test('S3 — the dashboard job pages open from the navigation', async ({ page }) => {
		const dashboard = new JobsDashboardPage(page);
		await dashboard.goto();

		await dashboard.openFromNavigation('Jobs');
		await dashboard.expectDashboard();
		await page.getByRole('link', { name: 'Failed', exact: false }).first().click();
		await dashboard.expectDashboard();
		await page.getByRole('link', { name: 'Succeeded', exact: false }).first().click();
		await dashboard.expectDashboard();
		await dashboard.openFromNavigation('Retries');
		await dashboard.expectDashboard();
		await expect(page.locator('.alert-danger')).toBeHidden();
	});
});
