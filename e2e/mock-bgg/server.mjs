// Mock of BoardGameGeek for the QA stack: the XML API 2, the login and the collection write calls.
// It has no dependencies. Tests steer it through the /__control endpoints.
import { readFileSync } from 'node:fs';
import { randomUUID } from 'node:crypto';
import { createServer } from 'node:http';
import { collectionXml, emptyItemsXml, searchXml, thingXml } from './xml.mjs';

const { games } = JSON.parse(readFileSync(new URL('../../test-data/bgg-games.json', import.meta.url), 'utf8'));

const validateGames = () => {
	const ids = new Set();
	for (const game of games) {
		if (ids.has(game.id)) {
			throw new Error(`Duplicate game id ${game.id} in the game data set`);
		}
		ids.add(game.id);
	}
	for (const game of games) {
		if (game.expansionOf !== undefined && !ids.has(game.expansionOf)) {
			throw new Error(`Game ${game.id} is an expansion of ${game.expansionOf}, which is not in the game data set`);
		}
	}
};
validateGames();

const PORT = Number(process.env.PORT ?? 4010);
const PUBLIC_BASE_URL = process.env.PUBLIC_BASE_URL ?? `http://localhost:${PORT}`;

const initialState = () => ({
	available: true,
	collection: [],
	addedItems: [],
	writes: [],
	acceptedLogins: 0,
	rejectedLogins: 0,
	rejectedWrites: 0,
	loginsRejected: false,
	nextCollId: 1000,
});

let state = initialState();

// Sessions outlive reset(), as they would on BGG. Only the expire control ends them.
const sessions = new Map();

const placeholderSvg = (label) =>
	`<svg xmlns="http://www.w3.org/2000/svg" width="200" height="200"><rect width="200" height="200" fill="#22263a"/>` +
	`<text x="100" y="108" fill="#e4e6f0" font-size="20" text-anchor="middle">${label}</text></svg>`;

const send = (res, status, body, contentType = 'application/json') => {
	res.writeHead(status, { 'Content-Type': contentType });
	res.end(typeof body === 'string' ? body : JSON.stringify(body));
};

const readJson = async (req) => {
	const chunks = [];
	for await (const chunk of req) {
		chunks.push(chunk);
	}
	const raw = Buffer.concat(chunks).toString('utf8');
	return raw ? JSON.parse(raw) : {};
};

const handleXmlApi = (url, res) => {
	if (!state.available) {
		return send(res, 503, 'BGG unavailable', 'text/plain');
	}

	const xml = 'application/xml';
	const endpoint = url.pathname.replace('/xmlapi2/', '');

	if (endpoint === 'search') {
		const query = (url.searchParams.get('query') ?? '').toLowerCase();
		const matches = games.filter((g) => !g.expansionOf && g.name.toLowerCase().includes(query));
		return send(res, 200, searchXml(matches), xml);
	}

	if (endpoint === 'thing') {
		const game = games.find((g) => String(g.id) === url.searchParams.get('id'));
		return send(res, 200, game ? thingXml(game, PUBLIC_BASE_URL) : emptyItemsXml, xml);
	}

	if (endpoint === 'collection') {
		const bggId = url.searchParams.get('id');
		if (bggId !== null) {
			return send(res, 200, collectionXml(state.addedItems.filter((i) => String(i.bggId) === bggId)), xml);
		}
		const wantsExpansions = url.searchParams.get('subtype') === 'boardgameexpansion';
		const items = state.collection.filter((i) => Boolean(i.expansion) === wantsExpansions);
		return send(res, 200, collectionXml(items), xml);
	}

	return send(res, 404, { error: `Unknown BGG endpoint: ${endpoint}` });
};

const cookieValue = (req, name) => {
	for (const pair of (req.headers.cookie ?? '').split(';')) {
		const [key, ...rest] = pair.trim().split('=');
		if (key === name) {
			return rest.join('=');
		}
	}
	return undefined;
};

/** The session of the request when mock BGG issued it, it is still valid and logins are accepted. */
const validSession = (req) => {
	const session = sessions.get(cookieValue(req, 'SessionID'));
	return !state.loginsRejected && session && session.valid ? session : undefined;
};

const handleLogin = async (req, res) => {
	const { credentials } = await readJson(req);
	if (state.loginsRejected || !credentials?.username || !credentials?.password) {
		state.rejectedLogins++;
		return send(res, 400, { error: 'Login rejected' });
	}
	const sessionId = randomUUID();
	sessions.set(sessionId, { username: credentials.username, valid: true });
	state.acceptedLogins++;
	res.writeHead(204, {
		'Set-Cookie': [
			`bggusername=${credentials.username}; Path=/`,
			'bggpassword=mock-password-cookie; Path=/',
			`SessionID=${sessionId}; Path=/`,
		],
	});
	return res.end();
};

const handleAdd = async (req, res) => {
	const body = await readJson(req);
	const session = validSession(req);
	if (!session) {
		state.rejectedWrites++;
		return send(res, 401, { error: 'Not logged in' });
	}
	const bggId = Number(body.item?.objectid);
	if (!bggId) {
		return send(res, 400, { error: 'objectid is required' });
	}
	const collId = state.nextCollId++;
	state.addedItems.push({ bggId, collId, name: body.item.objectname ?? '' });
	state.writes.push({ action: 'add', username: session.username, bggId, collId });
	return send(res, 200, { message: 'Item saved' });
};

const handleRemove = (req, url, res) => {
	const session = validSession(req);
	if (!session) {
		state.rejectedWrites++;
		return send(res, 401, { error: 'Not logged in' });
	}
	const collId = Number(url.pathname.replace('/api/collectionitem/', ''));
	state.addedItems = state.addedItems.filter((i) => i.collId !== collId);
	state.writes.push({ action: 'remove', username: session.username, collId });
	return send(res, 200, { message: 'Item removed from collection' });
};

const handleControl = async (req, url, res) => {
	if (url.pathname === '/__control/reset') {
		state = initialState();
		return send(res, 200, {});
	}

	if (url.pathname === '/__control/availability') {
		state.available = Boolean((await readJson(req)).available);
		return send(res, 200, {});
	}

	if (url.pathname === '/__control/reject-logins') {
		state.loginsRejected = Boolean((await readJson(req)).rejected);
		return send(res, 200, {});
	}

	if (url.pathname === '/__control/expire-sessions') {
		for (const session of sessions.values()) {
			session.valid = false;
		}
		return send(res, 200, {});
	}

	if (url.pathname === '/__control/collection') {
		state.collection = (await readJson(req)).items ?? [];
		return send(res, 200, {});
	}

	if (url.pathname === '/__control/calls') {
		return send(res, 200, {
			writes: state.writes,
			acceptedLogins: state.acceptedLogins,
			rejectedLogins: state.rejectedLogins,
			rejectedWrites: state.rejectedWrites,
		});
	}

	return send(res, 404, { error: 'Unknown control endpoint' });
};

createServer(async (req, res) => {
	try {
		const url = new URL(req.url, 'http://mock-bgg');

		if (url.pathname === '/health') {
			return send(res, 200, { status: 'ok' });
		}

		if (url.pathname.startsWith('/images/')) {
			const id = url.pathname.replace('/images/', '').replace('.svg', '');
			return send(res, 200, placeholderSvg(id), 'image/svg+xml');
		}

		if (url.pathname.startsWith('/xmlapi2/')) {
			return handleXmlApi(url, res);
		}

		if (url.pathname === '/login/api/v1' && req.method === 'POST') {
			return await handleLogin(req, res);
		}

		if (url.pathname === '/api/collectionitem' && req.method === 'POST') {
			return await handleAdd(req, res);
		}

		if (url.pathname.startsWith('/api/collectionitem/') && req.method === 'DELETE') {
			return handleRemove(req, url, res);
		}

		if (url.pathname.startsWith('/__control/')) {
			return await handleControl(req, url, res);
		}

		return send(res, 404, { error: 'Not found' });
	} catch (error) {
		console.error('[mock-bgg]', error);
		return send(res, 500, { error: String(error) });
	}
}).listen(PORT, () => console.log(`[mock-bgg] listening on port ${PORT}`));
