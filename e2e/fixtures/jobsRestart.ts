import { execFile } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { jobsUrl } from './urls';

const run = promisify(execFile);
const repositoryRoot = fileURLToPath(new URL('../..', import.meta.url));
const readyTimeoutMs = 120_000;

/** Restarts the jobs service of the QA stack, which drops its BGG session, and waits until the dashboard answers. */
export async function restartJobs(): Promise<void> {
	await run('docker', ['compose', '--profile', 'qa', 'restart', 'bgci-jobs'], { cwd: repositoryRoot });
	await waitUntilDashboardReady();
}

async function waitUntilDashboardReady(): Promise<void> {
	const deadline = Date.now() + readyTimeoutMs;
	let lastError = 'no response';

	while (Date.now() < deadline) {
		try {
			const response = await fetch(jobsUrl);
			if (response.ok) {
				return;
			}
			lastError = `GET ${jobsUrl} returned ${response.status}`;
		} catch (error) {
			lastError = String(error);
		}
		await new Promise((resolve) => setTimeout(resolve, 2000));
	}

	throw new Error(
		`The jobs dashboard at ${jobsUrl} was not ready ${readyTimeoutMs / 1000}s after the restart: ${lastError}`
	);
}
