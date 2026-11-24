import { expect, type Locator, type Page } from '@playwright/test';
import { jobsUrl } from '../fixtures/urls';

export type JobName = 'BGG add' | 'BGG remove';

const listTimeoutMs = 30_000;

/** The Hangfire dashboard of the jobs service. Every Hangfire selector of the suite is here. */
export class JobsDashboardPage {
	constructor(readonly page: Page) {}

	async openProcessing(): Promise<void> {
		await this.page.goto(`${jobsUrl}/jobs/processing`);
	}

	async openFailed(): Promise<void> {
		await this.page.goto(`${jobsUrl}/jobs/failed`);
	}

	/** Opens the Enqueued page and follows the link of the queue to its job list. */
	async openEnqueuedQueue(queue: string): Promise<void> {
		await this.page.goto(`${jobsUrl}/jobs/enqueued`);
		await this.page.getByRole('link', { name: queue, exact: true }).click();
		await this.page.waitForURL(new RegExp(`/jobs/enqueued/${queue}$`));
	}

	/** The rows of the job list that is open, for the jobs with the display name. */
	jobRows(name: JobName): Locator {
		return this.page.locator('tr.js-jobs-list-row', { hasText: name });
	}

	/** Reloads the Failed list until it holds a job with the display name. */
	async waitUntilFailed(name: JobName): Promise<void> {
		await expect
			.poll(
				async () => {
					await this.openFailed();
					return this.jobRows(name).count();
				},
				{ message: `A "${name}" job reaches the Failed list`, timeout: listTimeoutMs }
			)
			.toBeGreaterThan(0);
	}

	/** Reloads the Failed list until it holds no job with the display name. */
	async waitUntilNotFailed(name: JobName): Promise<void> {
		await expect
			.poll(
				async () => {
					await this.openFailed();
					return this.jobRows(name).count();
				},
				{ message: `No "${name}" job is in the Failed list`, timeout: listTimeoutMs }
			)
			.toBe(0);
	}

	/** Opens the details of the first job with the display name in the list that is open. */
	async openJob(name: JobName): Promise<void> {
		await this.jobRows(name).first().getByRole('link', { name: /^\d+$/ }).click();
		await this.page.waitForURL(/\/jobs\/details\/\d+/);
	}

	/** The state history of the job whose details are open: one card per state it went through. */
	get stateHistory(): Locator {
		return this.page.locator('.state-card');
	}

	/** The state card of the job whose details are open, for one state such as `Enqueued`. */
	stateCard(state: string): Locator {
		return this.stateHistory.filter({ has: this.page.locator('.state-card-title', { hasText: state }) });
	}

	async requeue(): Promise<void> {
		await this.page.getByRole('button', { name: /^\s*Requeue/ }).click();
	}

	/** Deletes the job whose details are open and accepts the confirmation. */
	async deleteJob(): Promise<void> {
		this.page.once('dialog', (dialog) => dialog.accept());
		await this.page.getByRole('button', { name: /^\s*Delete/ }).click();
	}
}
