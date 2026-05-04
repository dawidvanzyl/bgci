import { setAllGames } from './state.js';

const API = window.API_OVERRIDE ?? '/api';
export { API };

export async function loadConfig() {
	try {
		const res = await fetch(`${API}/config`);
		return await res.json();
	} catch {
		return { bggConfigured: false, bggReachable: false, bggSearchEnabled: false, bggSyncEnabled: false };
	}
}

export async function loadGames(onLoaded) {
	const res = await fetch(`${API}/games`);
	const games = await res.json();
	setAllGames(games);
	if (onLoaded) onLoaded();
}

export async function saveGame(payload, id) {
	const url    = id ? `${API}/games/${id}` : `${API}/games`;
	const method = id ? 'PUT' : 'POST';
	const res = await fetch(url, {
		method,
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify(payload),
	});
	if (!res.ok) throw new Error(await res.text());
}

export async function saveGameFromBgg(payload) {
	const res = await fetch(`${API}/games/from-bgg`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify(payload),
	});
	if (!res.ok) throw new Error(await res.text());
}

export async function deleteGame(id, onDeleted) {
	const res = await fetch(`${API}/games/${id}`, { method: 'DELETE' });
	if (!res.ok) throw new Error(await res.text());
	if (onDeleted) await onDeleted();
}

export async function searchBgg(q) {
	const res = await fetch(`${API}/bgg/search?query=${encodeURIComponent(q)}`);
	return res.json();
}

export async function fetchBggPreview(bggId) {
	const res = await fetch(`${API}/bgg/game/${bggId}/preview`);
	if (!res.ok) return null;
	return res.json();
}

export async function fetchBggExpansions(bggId) {
	const res = await fetch(`${API}/bgg/game/${bggId}/expansions`);
	if (!res.ok) throw new Error(await res.text());
	return res.json();
}

export async function saveExpansion(payload, parentGameId) {
	const res = await fetch(`${API}/games`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify({ ...payload, parentGameId }),
	});
	if (!res.ok) throw new Error(await res.text());
}

export async function saveExpansionFromBgg(bggId, parentGameId) {
	const previewRes = await fetch(`${API}/bgg/game/${bggId}/preview`);
	if (!previewRes.ok) throw new Error('Failed to fetch BGG data for expansion.');
	const preview = await previewRes.json();
	const res = await fetch(`${API}/games/from-bgg`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify({ ...preview, parentGameId }),
	});
	if (!res.ok) throw new Error(await res.text());
}
