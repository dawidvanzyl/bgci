import { expect, test } from '../../fixtures/base';
import { CollectionPage } from '../../pages/CollectionPage';
import { JobsDashboardPage } from '../../pages/JobsDashboardPage';

test.describe('Jobs service — the web port does not serve the dashboard', { tag: ['@86IT29'] }, () => {
	test.beforeEach(async ({ page }) => {
		await new JobsDashboardPage(page).waitUntilAnswering();
	});

	test("S1 — the dashboard's usual path on the web port is not the dashboard", async ({ page }) => {
		const dashboard = new JobsDashboardPage(page);
		await dashboard.goto();
		await dashboard.expectDashboard();

		await page.goto('/hangfire');

		await dashboard.expectNotDashboard();
	});

	test('S2 — the web root is the collection app, not the dashboard', async ({ page }) => {
		const collection = new CollectionPage(page);

		await collection.goto();

		await new JobsDashboardPage(page).expectNotDashboard();
	});

	test('S3 — the API path on the web port does not serve the dashboard', async ({ request }) => {
		const apiPath = await request.get('/api/hangfire');
		expect(apiPath.status()).toBe(404);
		expect(await apiPath.text()).not.toContain('Hangfire');

		const stats = await request.get('/hangfire/stats');
		const body = await stats.text();
		expect(body).not.toContain('Hangfire');
		expect(() => {
			const parsed: unknown = JSON.parse(body);
			if (typeof parsed !== 'object' || parsed === null) throw new Error('not an object');
		}).toThrow();
	});
});
