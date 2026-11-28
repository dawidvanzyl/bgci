import { expect, type Locator, type Page } from '@playwright/test';
import { jobsUrl } from '../fixtures/urls';

export type JobName = 'BGG add' | 'BGG remove';

const listTimeoutMs = 30_000;

/** The Hangfire dashboard of the jobs service. Every Hangfire selector of the suite is here. */
export class JobsDashboardPage {
	/** The newest job id on the Succeeded list when the test started. Hangfire keeps the Succeeded rows of earlier tests. */
	private succeededBaseline = 0;

	constructor(readonly page: Page) {}

	/** Remembers the newest Succeeded job, so `waitUntilSucceeded` counts only the jobs of this test. */
	async markSucceededBaseline(): Promise<void> {
		await this.openSucceeded();
		this.succeededBaseline = Math.max(0, ...(await this.listedJobIds()));
	}

	/** The ids of the jobs in the list that is open. */
	private async listedJobIds(name?: JobName): Promise<number[]> {
		const rows = name ? this.jobRows(name) : this.page.locator('tr.js-jobs-list-row');
		const links = await rows.getByRole('link', { name: /^#?\d+$/ }).allTextContents();
		return links.map((text) => Number(text.replace('#', '')));
	}

	async openProcessing(): Promise<void> {
		await this.page.goto(`${jobsUrl}/jobs/processing`);
	}

	async openSucceeded(): Promise<void> {
		await this.page.goto(`${jobsUrl}/jobs/succeeded`);
	}

	async openFailed(): Promise<void> {
		await this.page.goto(`${jobsUrl}/jobs/failed`);
	}

	/** Opens the Enqueued page and follows the link of the queue to its job list. */
	async openEnqueuedQueue(queue: string): Promise<void> {
		await this.page.goto(`${jobsUrl}/jobs/enqueued`);
		await this.followQueueLink(queue);
	}

	/** Follows the link of the queue on the Enqueued page that is open. */
	private async followQueueLink(queue: string): Promise<void> {
		await this.page.getByRole('link', { name: queue, exact: true }).click();
		await this.page.waitForURL(new RegExp(`/jobs/enqueued/${queue}$`));
	}

	/**
	 * The number of jobs with the display name that are Enqueued in the queue or Processing now.
	 * Hangfire lists a queue on the Enqueued page only while it holds a job, so a missing link counts as no job.
	 * The link is followed on the same page load that counted it. A second load can come after the last job has left
	 * the queue, and then it has no link to click.
	 */
	async activeJobCount(queue: string, name: JobName): Promise<number> {
		await this.page.goto(`${jobsUrl}/jobs/enqueued`);
		let enqueued = 0;
		if ((await this.page.getByRole('link', { name: queue, exact: true }).count()) > 0) {
			await this.followQueueLink(queue);
			enqueued = await this.jobRows(name).count();
		}
		await this.openProcessing();
		return enqueued + (await this.jobRows(name).count());
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

	/** Reloads the Succeeded list until it holds at least `count` jobs with the display name that are newer than the baseline. */
	async waitUntilSucceeded(name: JobName, count = 1): Promise<void> {
		await expect
			.poll(
				async () => {
					await this.openSucceeded();
					return (await this.listedJobIds(name)).filter((id) => id > this.succeededBaseline).length;
				},
				{ message: `At least ${count} "${name}" job reaches the Succeeded list`, timeout: listTimeoutMs }
			)
			.toBeGreaterThanOrEqual(count);
	}

	/** Opens the first Failed job with the display name and requeues it, as the owner does. */
	async retryFailed(name: JobName): Promise<void> {
		await this.openFailed();
		await this.openJob(name);
		await this.requeue();
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
		await this.jobRows(name)
			.first()
			.getByRole('link', { name: /^#?\d+$/ })
			.click();
		await this.page.waitForURL(/\/jobs\/details\/\d+/);
	}

	/** The state history of the job whose details are open: one card per state it went through. */
	get stateHistory(): Locator {
		return this.page.locator('.state-card');
	}

	/** The titles of the state cards of the job whose details are open, in the order the page shows them. */
	async stateTitles(): Promise<string[]> {
		const titles = await this.stateHistory.locator('.state-card-title').allTextContents();
		return titles.map((title) => title.replace(/^[\s\S]*\s(\w+)\s*$/, '$1'));
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
		const deleted = this.page.waitForResponse((response) => response.url().includes('/jobs/actions/delete/'));
		await this.page.getByRole('button', { name: /^\s*Delete/ }).click();
		await deleted;
		// Hangfire reloads the details page after the delete, and the reloaded history ends in Deleted.
		await expect(this.stateCard('Deleted')).toBeVisible();
	}
}
