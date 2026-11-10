import { defineConfig, devices } from '@playwright/test';

const webPort = process.env.QA_WEB_PORT ?? '15100';

export default defineConfig({
	testDir: './features',
	// The QA stack has one shared database, so specs run one at a time.
	fullyParallel: false,
	workers: 1,
	forbidOnly: !!process.env.CI,
	retries: process.env.CI ? 1 : 0,
	reporter: [['list'], ['html', { open: 'never' }]],
	globalSetup: './global-setup.ts',
	use: {
		baseURL: process.env.E2E_BASE_URL ?? `http://localhost:${webPort}`,
		trace: 'on-first-retry',
	},
	projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
