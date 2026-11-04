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

/** Client for the control endpoints of the mock BGG server (e2e/mock-bgg/server.mjs). */
export class MockBgg {
	constructor(private readonly baseUrl: string) {}

	async reset(): Promise<void> {
		await this.post('/__control/reset', {});
	}

	async setAvailable(available: boolean): Promise<void> {
		await this.post('/__control/availability', { available });
	}

	async setCollection(items: MockCollectionItem[]): Promise<void> {
		await this.post('/__control/collection', { items });
	}

	async writerCalls(): Promise<WriterCall[]> {
		const response = await fetch(`${this.baseUrl}/__control/calls`);
		return ((await response.json()) as { writer: WriterCall[] }).writer;
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
