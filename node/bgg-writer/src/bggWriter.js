'use strict';

const BGG_BASE_URL = 'https://boardgamegeek.com';
const LOGIN_URL = `${BGG_BASE_URL}/login/api/v1`;
const COLLECTION_ITEM_URL = `${BGG_BASE_URL}/api/collectionitem`;
const XMLAPI_BASE_URL = `${BGG_BASE_URL}/xmlapi2`;
const REQUEST_TIMEOUT_MS = 30000;
const LOOKUP_ATTEMPTS = 5;

const BGG_PASSWORD = process.env.BGG_PASSWORD;
const BGG_BEARER_TOKEN = process.env.BGG_BEARER_TOKEN;

let session = null;
let sessionLock = Promise.resolve();

function withLock(fn) {
	const next = sessionLock.then(fn, fn);
	sessionLock = next;
	return next;
}

class AuthError extends Error {
	constructor(message) {
		super(message);
		this.name = 'AuthError';
	}
}

function isChallenge(text) {
	return /just a moment|security verification|cf-chl/i.test(text);
}

/**
 * Sends a request to BGG. Throws an Error that names the failing step when BGG
 * answers with a Cloudflare challenge page or the request does not complete.
 *
 * @param {string} step Name of the step, used in error messages.
 * @param {string} url
 * @param {RequestInit} init
 * @returns {Promise<{ status: number, text: string, headers: Headers }>}
 */
async function send(step, url, init) {
	let response;
	try {
		response = await fetch(url, { ...init, signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) });
	} catch (err) {
		throw new Error(`BGG ${step} request failed: ${err.message}`);
	}

	const text = await response.text();
	console.log(`[bgg-writer] ${step}: ${init.method ?? 'GET'} ${new URL(url).pathname} -> ${response.status}`);

	if (isChallenge(text)) {
		throw new Error(`BGG Cloudflare challenge during ${step}`);
	}

	return { status: response.status, text, headers: response.headers };
}

function parseMessage(text) {
	try {
		return JSON.parse(text).message ?? null;
	} catch {
		return null;
	}
}

/**
 * Summarises a BGG response body for an error message. Uses the JSON error
 * message when there is one, and otherwise the first 200 characters of the body.
 *
 * @param {string} text
 * @returns {string}
 */
function describeBody(text) {
	try {
		const body = JSON.parse(text);
		const message = body.message ?? body.errors?.message ?? body.error;
		if (message) {
			return String(message);
		}
	} catch {
		// Not JSON — fall through to the raw text
	}

	const trimmed = text.replace(/s+/g, ' ').trim();
	return trimmed ? trimmed.slice(0, 200) : '(empty body)';
}

/**
 * Logs in to BGG through the JSON login API and returns the session cookies
 * as a single Cookie header value.
 *
 * @param {string} username
 * @param {string} password
 * @returns {Promise<string>}
 */
async function login(username, password) {
	console.log(`[bgg-writer] logging in via JSON API as user ${username}`);

	const result = await send('login', LOGIN_URL, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
		body: JSON.stringify({ credentials: { username, password } }),
	});

	if (result.status !== 204) {
		throw new Error(`BGG login failed with status ${result.status}: ${describeBody(result.text)}`);
	}

	const cookies = new Map();
	for (const setCookie of result.headers.getSetCookie()) {
		const pair = setCookie.split(';')[0];
		const separator = pair.indexOf('=');
		if (separator > 0) {
			cookies.set(pair.slice(0, separator), pair.slice(separator + 1));
		}
	}

	if (cookies.size === 0) {
		throw new Error('BGG login returned no session cookies');
	}

	cookies.set('bggusername', username);

	console.log('[bgg-writer] BGG login successful');
	return [...cookies].map(([name, value]) => `${name}=${value}`).join('; ');
}

/**
 * Ensures a cached login session exists, serialising session init and
 * invalidation through a mutex so concurrent requests cannot race.
 * The action itself runs outside the lock — only session lifecycle is serialised.
 * On AuthError from the action, rebuilds the session once and retries the action.
 * One writer instance serves one account: the session stays bound to the first username.
 *
 * @param {string} username
 * @param {function(string): Promise<T>} action Receives the Cookie header value.
 * @returns {Promise<T>}
 */
async function withBggSession(username, action) {
	if (!BGG_PASSWORD) {
		throw new Error('BGG_PASSWORD environment variable is not set');
	}

	const ensureSession = (staleCookie) => withLock(async () => {
		if (!session || session.cookie === staleCookie) {
			session = { username, cookie: await login(username, BGG_PASSWORD) };
		}
		return session.cookie;
	});

	const cookie = await ensureSession(null);

	try {
		return await action(cookie);
	} catch (err) {
		if (!(err instanceof AuthError)) {
			throw err;
		}

		console.log('[bgg-writer] auth failure detected — logging in again and retrying');
		return await action(await ensureSession(cookie));
	}
}

function writeHeaders(cookie) {
	return {
		'Accept': 'application/json',
		'X-KL-Ajax-Request': 'Ajax_Request',
		'Cookie': cookie,
	};
}

/**
 * Adds a game to the BGG collection and returns the collId.
 *
 * @param {string} username
 * @param {number} bggId
 * @param {string} name
 * @returns {Promise<number>}
 */
async function addToCollection(username, bggId, name) {
	return await withBggSession(username, async (cookie) => {
		console.log(`[bgg-writer] adding bggId ${bggId} to collection for user ${username}`);

		const result = await send('add', COLLECTION_ITEM_URL, {
			method: 'POST',
			headers: { ...writeHeaders(cookie), 'Content-Type': 'application/json' },
			body: JSON.stringify({
				item: {
					collid: 0,
					pp_currency: 'USD',
					cv_currency: 'USD',
					objecttype: 'thing',
					objectid: String(bggId),
					status: { own: true },
					objectname: name,
					acquisitiondate: null,
					invdate: null,
				},
			}),
		});

		if (result.status === 401 || result.status === 403) {
			throw new AuthError(`BGG add rejected with status ${result.status}: ${describeBody(result.text)}`);
		}

		const message = parseMessage(result.text);
		if (result.status !== 200 || message !== 'Item saved') {
			throw new Error(`BGG add failed with status ${result.status}: ${describeBody(result.text)}`);
		}

		// Resolve collId via XML API — retry a few times as BGG may lag
		const collId = await resolveCollId(username, bggId);

		if (!collId) {
			throw new Error(`BGG collection item id lookup found no collId for bggId ${bggId} after add`);
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
	return await withBggSession(username, async (cookie) => {
		console.log(`[bgg-writer] removing collId ${collId} from collection for user ${username}`);

		const result = await send('remove', `${COLLECTION_ITEM_URL}/${encodeURIComponent(collId)}`, {
			method: 'DELETE',
			headers: writeHeaders(cookie),
		});

		if (result.status === 401 || result.status === 403) {
			throw new AuthError(`BGG remove rejected with status ${result.status}: ${describeBody(result.text)}`);
		}

		const message = parseMessage(result.text);
		if (result.status !== 200 || message !== 'Item removed from collection') {
			throw new Error(`BGG remove failed with status ${result.status}: ${describeBody(result.text)}`);
		}

		console.log(`[bgg-writer] collId ${collId} removed`);
	});
}

/**
 * Resolves the BGG collection item ID (collId) for a given bggId by querying
 * the XML API with the bearer token.
 *
 * @param {string} username
 * @param {number} bggId
 * @returns {Promise<number|null>}
 */
async function resolveCollId(username, bggId) {
	if (!BGG_BEARER_TOKEN) {
		throw new Error('BGG collection item id lookup needs the BGG_BEARER_TOKEN environment variable');
	}

	const url = `${XMLAPI_BASE_URL}/collection?username=${encodeURIComponent(username)}&id=${bggId}&own=1`;

	for (let attempt = 1; attempt <= LOOKUP_ATTEMPTS; attempt++) {
		if (attempt > 1) {
			await new Promise((resolve) => setTimeout(resolve, attempt * 2000));
		}

		const result = await send('collection item id lookup', url, {
			headers: { 'Authorization': `Bearer ${BGG_BEARER_TOKEN}` },
		});

		if (result.status === 202) {
			console.log(`[bgg-writer] collection item id lookup attempt ${attempt}: BGG returned 202, retrying`);
			continue;
		}

		if (result.status !== 200) {
			throw new Error(`BGG collection item id lookup failed with status ${result.status}: ${describeBody(result.text)}`);
		}

		const match = result.text.match(/collid="(\d+)"/);
		if (match) {
			return parseInt(match[1], 10);
		}
	}

	return null;
}

module.exports = { addToCollection, removeFromCollection };
