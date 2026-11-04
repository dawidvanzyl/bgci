// Mock of the BGG XML API 2 and the bgg-writer sidecar, for the QA stack.
// It has no dependencies. Tests steer it through the /__control endpoints.
import { createServer } from 'node:http';
import { games } from './games.mjs';
import { collectionXml, emptyItemsXml, searchXml, thingXml } from './xml.mjs';

const PORT = Number(process.env.PORT ?? 4010);
const PUBLIC_BASE_URL = process.env.PUBLIC_BASE_URL ?? `http://localhost:${PORT}`;

const initialState = () => ({
	available: true,
	collection: [],
	writerCalls: [],
	nextCollId: 1000,
});

let state = initialState();

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
		const wantsExpansions = url.searchParams.get('subtype') === 'boardgameexpansion';
		const items = state.collection.filter((i) => Boolean(i.expansion) === wantsExpansions);
		return send(res, 200, collectionXml(items), xml);
	}

	return send(res, 404, { error: `Unknown BGG endpoint: ${endpoint}` });
};

const handleWriter = async (req, url, res) => {
	const body = await readJson(req);

	if (url.pathname === '/collection/add') {
		if (!body.username || !body.bggId) {
			return send(res, 400, { error: 'username and bggId are required' });
		}
		const collId = state.nextCollId++;
		state.writerCalls.push({ action: 'add', username: body.username, bggId: body.bggId, collId });
		return send(res, 200, { collId });
	}

	if (!body.username || !body.collId) {
		return send(res, 400, { error: 'username and collId are required' });
	}
	state.writerCalls.push({ action: 'remove', username: body.username, collId: body.collId });
	return send(res, 200, {});
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

	if (url.pathname === '/__control/collection') {
		state.collection = (await readJson(req)).items ?? [];
		return send(res, 200, {});
	}

	if (url.pathname === '/__control/calls') {
		return send(res, 200, { writer: state.writerCalls });
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

		if (url.pathname.startsWith('/collection/') && req.method === 'POST') {
			return await handleWriter(req, url, res);
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
