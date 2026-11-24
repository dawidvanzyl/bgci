/** The root of the jobs service of the QA stack. It serves the Hangfire dashboard. */
export const jobsUrl = process.env.E2E_JOBS_URL ?? `http://localhost:${process.env.QA_JOBS_DASHBOARD_PORT ?? 15104}`;
