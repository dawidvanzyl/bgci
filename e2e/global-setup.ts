import type { FullConfig } from '@playwright/test';

const timeoutMs = 120_000;

// Waits until the web server answers and the API has seen the mock BGG as reachable.
// `bggReachable` turns true after the API's first availability probe.
export default async function globalSetup(config: FullConfig): Promise<void> {
	const baseURL = config.projects[0].use.baseURL;
	const deadline = Date.now() + timeoutMs;
	let lastError = 'no response';

	while (Date.now() < deadline) {
		try {
			const response = await fetch(`${baseURL}/api/config`);
			if (response.ok) {
				const body = (await response.json()) as { bggReachable?: boolean };
				if (body.bggReachable) {
					return;
				}
				lastError = 'API is up but BGG is not reachable yet';
			} else {
				lastError = `GET /api/config returned ${response.status}`;
			}
		} catch (error) {
			lastError = String(error);
		}
		await new Promise((resolve) => setTimeout(resolve, 2000));
	}

	throw new Error(`The QA stack at ${baseURL} was not ready after ${timeoutMs / 1000}s: ${lastError}`);
}
