import { execFile } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const run = promisify(execFile);
const repositoryRoot = fileURLToPath(new URL('../..', import.meta.url));
const readyTimeoutMs = 120_000;

/** Restarts the API service of the QA stack and waits until it answers through the web proxy and sees BGG. */
export async function restartApi(baseURL: string): Promise<void> {
	await run('docker', ['compose', '--profile', 'qa', 'restart', 'bgci-api'], { cwd: repositoryRoot });
	await waitUntilApiReady(baseURL);
}

async function waitUntilApiReady(baseURL: string): Promise<void> {
	const deadline = Date.now() + readyTimeoutMs;
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

	throw new Error(`The API at ${baseURL} was not ready ${readyTimeoutMs / 1000}s after the restart: ${lastError}`);
}
