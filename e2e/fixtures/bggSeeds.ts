import { expect, type APIRequestContext } from '@playwright/test';
import type { CollectionPage } from '../pages/CollectionPage';
import type { JobsDashboardPage } from '../pages/JobsDashboardPage';
import type { BggAttempt, MockBgg, MockCollectionItem } from './mockBgg';

export const catanBggId = 13;
export const azulBggId = 230802;
export const actionTestBggId = 3179;
export const seafarersBggId = 325;
export const citiesAndKnightsBggId = 926;
export const tradersAndBarbariansBggId = 27760;

export const catanItem: MockCollectionItem = { bggId: catanBggId, collId: 5001, name: 'Catan', year: 1995 };
export const azulItem: MockCollectionItem = { bggId: azulBggId, collId: 5001, name: 'Azul', year: 2017 };
export const seafarersItem: MockCollectionItem = {
	bggId: seafarersBggId,
	collId: 5002,
	name: 'Catan: Seafarers',
	year: 1997,
	expansion: true,
};

export const catanSeafarersName = 'Catan: Seafarers';

export const jobTimeoutMs = 30_000;

export type BggStatus = 'pending' | 'sent' | 'failed';

/** Polls the API until the named game has the BGG status. It is a seed wait, never the assertion of a scenario. */
export async function waitForBggStatus(request: APIRequestContext, name: string, status: BggStatus): Promise<void> {
	await expect
		.poll(
			async () => {
				const games = (await (await request.get('/api/games')).json()) as { name: string; bggStatus: string | null }[];
				return games.find((game) => game.name === name)?.bggStatus;
			},
			{ message: `"${name}" gets the BGG status ${status}`, timeout: jobTimeoutMs }
		)
		.toBe(status);
}

/** Runs the sync and waits until the game is listed. The sync imports the game with the status sent. */
export async function importBySync(collection: CollectionPage, name: string): Promise<void> {
	await collection.sync();
	await expect(collection.card(name)).toBeVisible();
}

/** Adds a game from a BGG search and waits until the card is listed. */
export async function addFromBgg(collection: CollectionPage, query: string, name: string): Promise<void> {
	await collection.importFromBgg(query, name);
	await expect(collection.card(name)).toBeVisible();
}

export const addCatanFromBgg = (collection: CollectionPage): Promise<void> => addFromBgg(collection, 'catan', 'Catan');
export const addAzulFromBgg = (collection: CollectionPage): Promise<void> => addFromBgg(collection, 'azul', 'Azul');
export const addActionTestFromBgg = (collection: CollectionPage): Promise<void> =>
	addFromBgg(collection, 'action', 'Action Test');

/** Sent game: adds the game from BGG and waits until its add job has finished. */
export async function seedSentGame(
	collection: CollectionPage,
	request: APIRequestContext,
	query: string,
	name: string
): Promise<void> {
	await addFromBgg(collection, query, name);
	await waitForBggStatus(request, name, 'sent');
}

/**
 * Failed game: three writes fail, the game is added, and the wait ends when the game has the status failed
 * and the BGG add job is in the Failed list. That state is terminal, so no further attempt follows it.
 */
export async function seedFailedGame(
	collection: CollectionPage,
	request: APIRequestContext,
	mockBgg: MockBgg,
	dashboard: JobsDashboardPage,
	query: string,
	name: string
): Promise<void> {
	await mockBgg.failNextWrites(3);
	await addFromBgg(collection, query, name);
	await waitForBggStatus(request, name, 'failed');
	await dashboard.waitUntilFailed('BGG add');
}

/**
 * Failed remove: Catan is sent, the next three writes fail, the game is removed, and the wait ends when a BGG remove
 * job is in the Failed list. That state is terminal, so no further attempt follows it.
 */
export async function seedFailedRemove(
	collection: CollectionPage,
	request: APIRequestContext,
	mockBgg: MockBgg,
	dashboard: JobsDashboardPage,
	query: string,
	name: string
): Promise<void> {
	await collection.goto();
	await seedSentGame(collection, request, query, name);
	await mockBgg.failNextWrites(3);
	await collection.remove(name);
	await dashboard.waitUntilFailed('BGG remove');
}

/** Failed expansion: as a failed game, but the expansion is added from the Known BGG expansions of the open Info dialog. */
export async function seedFailedKnownExpansion(
	collection: CollectionPage,
	request: APIRequestContext,
	mockBgg: MockBgg,
	dashboard: JobsDashboardPage,
	name: string
): Promise<void> {
	await mockBgg.failNextWrites(3);
	await collection.addKnownBggExpansion(name);
	await waitForBggStatus(request, name, 'failed');
	await dashboard.waitUntilFailed('BGG add');
}

/** Opens Catan's Info dialog on the Expansions tab. */
export async function openCatanExpansions(collection: CollectionPage): Promise<void> {
	await collection.openInfo('Catan');
	await collection.openExpansionsTab();
}

/** Opens Catan's Expansions tab and adds a known BGG expansion. */
export async function addKnownExpansionToCatan(collection: CollectionPage, name: string): Promise<void> {
	await openCatanExpansions(collection);
	await collection.addKnownBggExpansion(name);
}

/** Sent expansion of Catan: adds the expansion from the Known BGG expansions and waits until its add job has finished. */
export async function seedSentExpansion(
	collection: CollectionPage,
	request: APIRequestContext,
	name: string
): Promise<void> {
	await addKnownExpansionToCatan(collection, name);
	await waitForBggStatus(request, name, 'sent');
}

/** The collection item id of the one accepted add of the BGG id. */
export async function acceptedAddCollId(mockBgg: MockBgg, bggId: number): Promise<number> {
	const adds = (await mockBgg.acceptedWrites()).filter((write) => write.action === 'add' && write.bggId === bggId);
	expect(adds).toHaveLength(1);
	return adds[0].collId;
}

/** Polls mock BGG until a write of the kind has started and has no end time, which means it is held. */
export async function waitUntilWriteHeld(
	mockBgg: MockBgg,
	action: 'add' | 'remove',
	matches: (attempt: BggAttempt) => boolean
): Promise<void> {
	await expect
		.poll(async () => (await mockBgg.attempts(action, matches)).some((attempt) => attempt.endedAt === null), {
			message: `A ${action} write is held at mock BGG`,
			timeout: jobTimeoutMs,
		})
		.toBe(true);
}

export const isAddOf =
	(bggId: number) =>
	(attempt: BggAttempt): boolean =>
		attempt.bggId === bggId;

export const isRemoveOf =
	(collId: number) =>
	(attempt: BggAttempt): boolean =>
		attempt.collId === collId;

/** The accepted writes as `action:id` text: the BGG id for an add, the collection item id for a remove. */
export async function acceptedWriteLabels(mockBgg: MockBgg): Promise<string[]> {
	return (await mockBgg.acceptedWrites()).map((write) =>
		write.action === 'add' ? `add:${write.bggId}` : `remove:${write.collId}`
	);
}

export const handGameName = 'Kitchen Table Classic';

/**
 * The games of the status scenarios, in this order: Sent Catan, Failed Azul, an optional hand game, and Pending Action Test.
 * The write of Action Test is held (`holdWrites('next')`) until the spec releases it. The page is reloaded at the end,
 * so every card shows the stored status.
 */
export async function seedStatusGames(
	collection: CollectionPage,
	request: APIRequestContext,
	mockBgg: MockBgg,
	dashboard: JobsDashboardPage,
	options: { withHandGame: boolean }
): Promise<void> {
	await collection.goto();
	await seedSentGame(collection, request, 'catan', 'Catan');
	await seedFailedGame(collection, request, mockBgg, dashboard, 'azul', 'Azul');
	if (options.withHandGame) {
		await collection.addGame({ name: handGameName });
	}
	await mockBgg.holdWrites('next');
	await addActionTestFromBgg(collection);
	await waitUntilWriteHeld(mockBgg, 'add', isAddOf(actionTestBggId));
	await collection.reload();
}
