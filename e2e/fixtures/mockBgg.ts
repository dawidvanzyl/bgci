/** A write that mock BGG accepted. The username comes from the login of the session. */
export interface BggWrite {
	action: 'add' | 'remove';
	username: string;
	bggId?: number;
	collId: number;
}

/** One add or remove that reached mock BGG, accepted or not. The times are milliseconds since the epoch. */
export interface BggAttempt {
	action: 'add' | 'remove';
	/** Null when the session was not valid. */
	username: string | null;
	bggId?: number;
	/** Known for an add once it is accepted; always known for a remove. */
	collId: number | null;
	/** Null while the write is held. */
	outcome: 'accepted' | 'failed' | 'rejected' | null;
	startedAt: number;
	/** Null while the write is held. */
	endedAt: number | null;
}

export interface BggReport {
	attempts: BggAttempt[];
	maxConcurrentWrites: number;
	writes: BggWrite[];
	acceptedLogins: number;
	rejectedLogins: number;
	rejectedWrites: number;
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

	/**
	 * While rejected, every login gets a 400 and every add and remove gets a 401, whatever session it carries.
	 * `reset()` turns it off.
	 */
	async setLoginsRejected(rejected: boolean): Promise<void> {
		await this.post('/__control/reject-logins', { rejected });
	}

	/** Expires every session issued so far. The next add or remove with one gets a 401. A new login works. */
	async expireSessions(): Promise<void> {
		await this.post('/__control/expire-sessions', {});
	}

	/**
	 * `all`: every write with a valid session waits until `releaseWrites()`.
	 * `next`: only the next such write waits.
	 */
	async holdWrites(mode: 'all' | 'next'): Promise<void> {
		await this.post('/__control/hold-writes', { mode });
	}

	/** Answers every held write as it would have been answered without the hold, and turns the hold off. */
	async releaseWrites(): Promise<void> {
		await this.post('/__control/release-writes', {});
	}

	/** The next `count` writes with a valid session answer 500 and change nothing. */
	async failNextWrites(count: number): Promise<void> {
		await this.post('/__control/fail-next-writes', { count });
	}

	/** While disabled, the collection item lookup answers an empty collection. `reset()` enables it. */
	async setCollectionLookup(enabled: boolean): Promise<void> {
		await this.post('/__control/collection-lookup', { enabled });
	}

	async setCollection(items: MockCollectionItem[]): Promise<void> {
		await this.post('/__control/collection', { items });
	}

	async report(): Promise<BggReport> {
		const response = await fetch(`${this.baseUrl}/__control/calls`);
		return (await response.json()) as BggReport;
	}

	/** The write attempts of the given kind that have started, in start order. */
	async attempts(
		action: 'add' | 'remove',
		predicate: (attempt: BggAttempt) => boolean = () => true
	): Promise<BggAttempt[]> {
		return (await this.report()).attempts.filter((attempt) => attempt.action === action && predicate(attempt));
	}

	/** The adds and removes that mock BGG accepted, in order. */
	async acceptedWrites(): Promise<BggWrite[]> {
		return (await this.report()).writes;
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
