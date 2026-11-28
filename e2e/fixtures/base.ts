import { test as base, type APIRequestContext } from '@playwright/test';
import { MockBgg } from './mockBgg';
import { JobsDashboardPage } from '../pages/JobsDashboardPage';
import { jobsUrl } from './urls';

const mockUrl = process.env.E2E_MOCK_URL ?? `http://localhost:${process.env.QA_MOCK_PORT ?? 15103}`;

// The jobs reset waits up to 35 seconds for a running job to end.
const jobsResetTimeoutMs = 45_000;

async function removeAllGames(request: APIRequestContext): Promise<void> {
	const games = (await (await request.get('/api/games')).json()) as { id: string }[];
	for (const game of games) {
		await request.delete(`/api/games/${game.id}`);
	}
}

/** Control of the jobs service of the QA stack. */
export class Jobs {
	constructor(private readonly request: APIRequestContext) {}

	/** Deletes every BGG job and every job record, and returns when no job body runs. */
	async reset(): Promise<void> {
		const response = await this.request.post(`${jobsUrl}/qa/reset`, { timeout: jobsResetTimeoutMs });
		if (response.status() !== 204) {
			throw new Error(`The jobs reset returned ${response.status()}`);
		}
	}
}

interface Fixtures {
	mockBgg: MockBgg;
	jobs: Jobs;
	jobsDashboard: JobsDashboardPage;
	cleanState: void;
}

export const test = base.extend<Fixtures>({
	mockBgg: async ({}, use) => {
		await use(new MockBgg(mockUrl));
	},

	jobs: async ({ request }, use) => {
		await use(new Jobs(request));
	},

	// Every test starts with an empty collection, no BGG job and a reset mock BGG.
	// Games go first, so the remove jobs they queue are cleared with the rest. The first mock reset answers a
	// held write from the last test, so its job ends at once. The jobs reset then removes every job and waits
	// until none runs. The second mock reset clears any write that a job sent before it ended.
	// The dashboard has its own page, so a spec keeps the collection page open while it reads the dashboard.
	// The Succeeded list keeps the rows of earlier tests, so the page object notes the newest one first.
	jobsDashboard: async ({ context, cleanState }, use) => {
		void cleanState;
		const dashboardPage = await context.newPage();
		const dashboard = new JobsDashboardPage(dashboardPage);
		await dashboard.markSucceededBaseline();
		await use(dashboard);
		await dashboardPage.close();
	},

	cleanState: [
		async ({ request, mockBgg, jobs }, use) => {
			await removeAllGames(request);
			await mockBgg.reset();
			await jobs.reset();
			await mockBgg.reset();
			await use();
		},
		{ auto: true, timeout: 120_000 },
	],
});

export { expect } from './expect';
