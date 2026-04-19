'use strict';

const { firefox } = require('playwright');

const BGG_BASE_URL = 'https://boardgamegeek.com';
const COLLECTION_WRITE_URL = `${BGG_BASE_URL}/geekcollection.php`;
const XMLAPI_BASE_URL = `${BGG_BASE_URL}/xmlapi2`;

let cachedBrowser = null;
let cachedContext = null;

class AuthError extends Error {
	constructor(message) {
		super(message);
		this.name = 'AuthError';
	}
}

/**
 * Logs in to BGG via the JSON API from within the browser context.
 * Sets SessionID cookie (via API) and bggusername cookie (injected manually).
 *
 * @param {import('playwright').BrowserContext} context
 * @param {string} username
 * @param {string} password
 * @returns {Promise<void>}
 */
async function login(context, username, password) {
	const page = await context.newPage();

	try {
		console.log('[bgg-writer] navigating to BGG homepage to satisfy Cloudflare');
		await page.goto(BGG_BASE_URL, { waitUntil: 'networkidle' });

		console.log('[bgg-writer] logging in via JSON API');
		const loginOk = await page.evaluate(async ({ credentials, bggBaseUrl }) => {
			const response = await fetch('/login/api/v1', {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json',
					'Origin': bggBaseUrl,
					'X-KL-Ajax-Request': 'Ajax_Request',
				},
				body: JSON.stringify({ credentials }),
			});
			return response.ok;
		}, { credentials: { username, password }, bggBaseUrl: BGG_BASE_URL });

		if (!loginOk) {
			throw new AuthError('BGG login failed');
		}

		await context.addCookies([{
			name: 'bggusername',
			value: username,
			domain: 'boardgamegeek.com',
			path: '/',
		}]);

		console.log('[bgg-writer] BGG login successful');
	} finally {
		await page.close();
	}
}

/**
 * Ensures a cached authenticated browser context exists, launching and
 * logging in if necessary. On AuthError from the action, invalidates the
 * cache, re-logs in, and retries the action once.
 *
 * @param {string} username
 * @param {function(import('playwright').Page): Promise<T>} action
 * @returns {Promise<T>}
 */
async function withBggSession(username, action) {
	const password = process.env.BGG_PASSWORD;
	if (!password) {
		throw new Error('BGG_PASSWORD environment variable is not set');
	}

	if (!cachedBrowser || !cachedContext) {
		console.log('[bgg-writer] launching browser and creating session');
		cachedBrowser = await firefox.launch({ headless: true });
		cachedContext = await cachedBrowser.newContext();
		await login(cachedContext, username, password);
	}

	const page = await cachedContext.newPage();
	await page.goto(BGG_BASE_URL, { waitUntil: 'domcontentloaded' });

	try {
		return await action(page);
	} catch (err) {
		if (!(err instanceof AuthError)) {
			throw err;
		}

		console.log('[bgg-writer] auth failure detected — re-logging in and retrying');

		await page.close();

		if (cachedBrowser) {
			await cachedBrowser.close().catch(() => {});
		}
		cachedBrowser = await firefox.launch({ headless: true });
		cachedContext = await cachedBrowser.newContext();
		await login(cachedContext, username, password);

		const retryPage = await cachedContext.newPage();
		await retryPage.goto(BGG_BASE_URL, { waitUntil: 'domcontentloaded' });
		try {
			return await action(retryPage);
		} finally {
			await retryPage.close();
		}
	} finally {
		if (!page.isClosed()) {
			await page.close();
		}
	}
}

/**
 * Adds a game to the BGG collection and returns the collId.
 *
 * @param {string} username
 * @param {number} bggId
 * @returns {Promise<number>}
 */
async function addToCollection(username, bggId) {
	return await withBggSession(username, async (page) => {
		console.log(`[bgg-writer] adding bggId ${bggId} to collection for user ${username}`);

		const result = await page.evaluate(
			async ({ url, bggId }) => {
				const body = new URLSearchParams({
					objecttype: 'thing',
					objectid: String(bggId),
					addowned: 'true',
					addwish: 'false',
					wishlistpriority: '1',
					force: 'true',
					ajax: '1',
					action: 'additem',
				});

				const response = await fetch(url, {
					method: 'POST',
					headers: {
						'Content-Type': 'application/x-www-form-urlencoded',
						'X-Requested-With': 'XMLHttpRequest',
						'X-KL-Ajax-Request': 'Ajax_Request',
					},
					body: body.toString(),
				});

				return { status: response.status, ok: response.ok };
			},
			{ url: COLLECTION_WRITE_URL, bggId }
		);

		if (result.status === 401 || result.status === 403) {
			throw new AuthError(`BGG geekcollection.php add rejected with status ${result.status}`);
		}

		if (!result.ok) {
			throw new Error(`BGG geekcollection.php add failed with status ${result.status}`);
		}

		// Resolve collId via XML API — retry a few times as BGG may lag
		const collId = await resolveCollId(page, username, bggId);

		if (!collId) {
			throw new Error(`Could not resolve collId for bggId ${bggId} after add`);
		}

		console.log(`[bgg-writer] bggId ${bggId} added, collId=${collId}`);
		return collId;
	});
}

/**
 * Removes a game from the BGG collection by collId.
 *
 * @param {string} username
 * @param {number} collId
 * @returns {Promise<void>}
 */
async function removeFromCollection(username, collId) {
	return await withBggSession(username, async (page) => {
		console.log(`[bgg-writer] removing collId ${collId} from collection for user ${username}`);

		const result = await page.evaluate(
			async ({ url, collId }) => {
				const body = new URLSearchParams({
					ajax: '1',
					action: 'delete',
					collid: String(collId),
				});

				const response = await fetch(url, {
					method: 'POST',
					headers: {
						'Content-Type': 'application/x-www-form-urlencoded',
						'X-Requested-With': 'XMLHttpRequest',
						'X-KL-Ajax-Request': 'Ajax_Request',
					},
					body: body.toString(),
				});

				return { status: response.status, ok: response.ok };
			},
			{ url: COLLECTION_WRITE_URL, collId }
		);

		if (result.status === 401 || result.status === 403) {
			throw new AuthError(`BGG geekcollection.php delete rejected with status ${result.status}`);
		}

		if (!result.ok) {
			throw new Error(`BGG geekcollection.php delete failed with status ${result.status}`);
		}

		console.log(`[bgg-writer] collId ${collId} removed`);
	});
}

/**
 * Resolves the BGG collection item ID (collId) for a given bggId by querying
 * the XML API from within the authenticated browser context.
 *
 * @param {import('playwright').Page} page
 * @param {string} username
 * @param {number} bggId
 * @returns {Promise<number|null>}
 */
async function resolveCollId(page, username, bggId) {
	const url = `${XMLAPI_BASE_URL}/collection?username=${encodeURIComponent(username)}&id=${bggId}&own=1`;

	for (let attempt = 1; attempt <= 5; attempt++) {
		if (attempt > 1) {
			await new Promise((resolve) => setTimeout(resolve, attempt * 2000));
		}

		const xml = await page.evaluate(async (fetchUrl) => {
			const response = await fetch(fetchUrl);
			if (response.status === 202) return null;
			return response.text();
		}, url);

		if (!xml) {
			console.log(`[bgg-writer] resolveCollId attempt ${attempt}: BGG returned 202, retrying`);
			continue;
		}

		const match = xml.match(/collid="(\d+)"/);
		if (match) {
			return parseInt(match[1], 10);
		}
	}

	return null;
}

module.exports = { addToCollection, removeFromCollection };
