import type { FullConfig } from '@playwright/test';
import { jobsUrl } from './fixtures/urls';

const timeoutMs = 120_000;

// Waits until the web server answers, the API has seen the mock BGG as reachable and the jobs dashboard answers.
// `bggReachable` turns true after the API's first availability probe.
export default async function globalSetup(config: FullConfig): Promise<void> {
	const baseURL = config.projects[0].use.baseURL;
	const deadline = Date.now() + timeoutMs;
	let lastError = 'no response';
	let apiReady = false;

	while (Date.now() < deadline) {
		try {
			if (!apiReady) {
				const response = await fetch(`${baseURL}/api/config`);
				if (response.ok) {
					const body = (await response.json()) as { bggReachable?: boolean };
					apiReady = Boolean(body.bggReachable);
					lastError = 'API is up but BGG is not reachable yet';
				} else {
					lastError = `GET /api/config returned ${response.status}`;
				}
			}
			if (apiReady) {
				const dashboard = await fetch(jobsUrl);
				if (dashboard.ok) {
					return;
				}
				lastError = `GET ${jobsUrl} returned ${dashboard.status}`;
			}
		} catch (error) {
			lastError = String(error);
		}
		await new Promise((resolve) => setTimeout(resolve, 2000));
	}

	throw new Error(`The QA stack at ${baseURL} was not ready after ${timeoutMs / 1000}s: ${lastError}`);
}
