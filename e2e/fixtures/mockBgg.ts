export interface WriterCall {
	action: 'add' | 'remove';
	username: string;
	bggId?: number;
	collId: number;
}

export interface MockCollectionItem {
	bggId: number;
	collId: number;
	name: string;
	year?: number;
	expansion?: boolean;
}

export interface BggThing {
	id: number;
	name: string;
	year: number;
	minPlayers: number;
	maxPlayers: number;
	playTime: number;
	minPlayTime: number;
	maxPlayTime: number;
	minAge: number;
	rating: number;
	weight: number;
}

export interface BggSearchItem {
	id: number;
	name: string;
}

const unescapeXml = (value: string): string =>
	value
		.replace(/&quot;/g, '"')
		.replace(/&lt;/g, '<')
		.replace(/&gt;/g, '>')
		.replace(/&amp;/g, '&');

const items = (xml: string): string[] => xml.match(/<item\b[\s\S]*?<\/item>/g) ?? [];

/** Reads the `value` attribute of the first element with the tag, or the named attribute when given. */
const attribute = (xml: string, tag: string, name = 'value'): string => {
	const match = new RegExp(`<${tag}\\b[^>]*?\\b${name}="([^"]*)"`).exec(xml);
	if (!match) {
		throw new Error(`BGG XML has no ${tag} ${name}`);
	}
	return unescapeXml(match[1]);
};

const primaryName = (xml: string): string => {
	const match = /<name\b[^>]*?\btype="primary"[^>]*?\bvalue="([^"]*)"/.exec(xml);
	if (!match) {
		throw new Error('BGG XML has no primary name');
	}
	return unescapeXml(match[1]);
};

/** Client for the control endpoints of the mock BGG server (e2e/mock-bgg/server.mjs). */
export class MockBgg {
	constructor(private readonly baseUrl: string) {}

	async reset(): Promise<void> {
		await this.post('/__control/reset', {});
	}

	async setAvailable(available: boolean): Promise<void> {
		await this.post('/__control/availability', { available });
	}

	/** While failing, the writer's add and remove calls answer 500 and are not recorded. `reset()` clears it. */
	async setWriterFailing(failing: boolean): Promise<void> {
		await this.post('/__control/writer', { failing });
	}

	async setCollection(items: MockCollectionItem[]): Promise<void> {
		await this.post('/__control/collection', { items });
	}

	async writerCalls(): Promise<WriterCall[]> {
		const response = await fetch(`${this.baseUrl}/__control/calls`);
		return ((await response.json()) as { writer: WriterCall[] }).writer;
	}

	/** Calls the BGG thing endpoint and returns every item in the response. */
	async thing(id: number): Promise<BggThing[]> {
		const xml = await this.getXml(`/xmlapi2/thing?id=${id}`);
		return items(xml).map((item) => ({
			id: Number(attribute(item, 'item', 'id')),
			name: primaryName(item),
			year: Number(attribute(item, 'yearpublished')),
			minPlayers: Number(attribute(item, 'minplayers')),
			maxPlayers: Number(attribute(item, 'maxplayers')),
			playTime: Number(attribute(item, 'playingtime')),
			minPlayTime: Number(attribute(item, 'minplaytime')),
			maxPlayTime: Number(attribute(item, 'maxplaytime')),
			minAge: Number(attribute(item, 'minage')),
			rating: Number(attribute(item, 'average')),
			weight: Number(attribute(item, 'averageweight')),
		}));
	}

	/** Calls the BGG search endpoint and returns every item in the response. */
	async search(query: string): Promise<BggSearchItem[]> {
		const xml = await this.getXml(`/xmlapi2/search?query=${encodeURIComponent(query)}`);
		return items(xml).map((item) => ({
			id: Number(attribute(item, 'item', 'id')),
			name: primaryName(item),
		}));
	}

	private async getXml(path: string): Promise<string> {
		const response = await fetch(`${this.baseUrl}${path}`);
		if (!response.ok) {
			throw new Error(`Mock BGG ${path} returned ${response.status}`);
		}
		return response.text();
	}

	private async post(path: string, body: unknown): Promise<void> {
		const response = await fetch(`${this.baseUrl}${path}`, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify(body),
		});
		if (!response.ok) {
			throw new Error(`Mock BGG ${path} returned ${response.status}`);
		}
	}
}
