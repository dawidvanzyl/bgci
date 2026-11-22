import { expect, type Locator, type Page } from '@playwright/test';
import { jobsUrl } from '../fixtures/base';

const dashboardTitle = /Hangfire Dashboard/;
const navigationEntries = ['Jobs', 'Retries', 'Recurring Jobs', 'Servers'];
const readyTimeoutMs = 60_000;

export class JobsDashboardPage {
	constructor(readonly page: Page) {}

	get navigation(): Locator {
		return this.page.getByRole('navigation');
	}

	navigationEntry(name: string): Locator {
		return this.navigation.getByRole('link', { name, exact: true });
	}

	/** Waits until the jobs service answers with a success status, so the jobs service has finished starting. */
	async waitUntilAnswering(): Promise<void> {
		await expect
			.poll(
				async () =>
					this.page.request.get(jobsUrl).then(
						(response) => response.status(),
						() => 0
					),
				{
					timeout: readyTimeoutMs,
				}
			)
			.toBe(200);
	}

	async goto(): Promise<number> {
		const response = await this.page.goto(jobsUrl);
		return response?.status() ?? 0;
	}

	async openFromNavigation(name: string): Promise<void> {
		await this.navigationEntry(name).click();
	}

	async expectDashboard(): Promise<void> {
		await expect(this.page).toHaveTitle(dashboardTitle);
		for (const entry of navigationEntries) {
			await expect(this.navigationEntry(entry)).toBeVisible();
		}
	}

	async expectNotDashboard(): Promise<void> {
		await expect(this.page).not.toHaveTitle(dashboardTitle);
		await expect(this.page.getByText('Recurring Jobs')).toBeHidden();
		await expect(this.page.getByText('Servers', { exact: true })).toBeHidden();
	}

	get serverRows(): Locator {
		return this.page.locator('table tbody tr').filter({ has: this.page.locator('td') });
	}

	get serverQueues(): Locator {
		return this.page.locator('table tbody tr td .label');
	}
}
