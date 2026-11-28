import type { APIRequestContext } from '@playwright/test';
import { expect } from './expect';
import type { CollectionPage } from '../pages/CollectionPage';
import type { JobsDashboardPage } from '../pages/JobsDashboardPage';
import type { MockBgg } from './mockBgg';
import {
	acceptedAddCollId,
	acceptedWriteLabels,
	catanBggId,
	catanSeafarersName,
	isRemoveOf,
	jobTimeoutMs,
	seafarersBggId,
	seedSentExpansion,
	seedSentGame,
	waitUntilWriteHeld,
} from './bggSeeds';

const refusalTail = `can't be added yet. Removing it from your BGG collection isn't finished. Try again later.`;

export const catanRefusal = `"Catan" ${refusalTail}`;
export const seafarersRefusal = `The expansion "${catanSeafarersName}" ${refusalTail}`;

/** Catan is sent and then removed, and mock BGG holds the remove. Returns the collection item id of the remove. */
export async function seedHeldRemove(
	collection: CollectionPage,
	request: APIRequestContext,
	mockBgg: MockBgg
): Promise<number> {
	await collection.goto();
	await seedSentGame(collection, request, 'catan', 'Catan');
	const collId = await acceptedAddCollId(mockBgg, catanBggId);
	await mockBgg.holdWrites('all');
	await collection.remove('Catan');
	await waitUntilWriteHeld(mockBgg, 'remove', isRemoveOf(collId));
	return collId;
}

/**
 * Catan (sent) owns Catan: Seafarers (sent). The expansion is removed and mock BGG holds the remove.
 * Catan's Expansions tab is left open. Returns the collection item id of the remove.
 */
export async function seedHeldExpansionRemove(
	collection: CollectionPage,
	request: APIRequestContext,
	mockBgg: MockBgg
): Promise<number> {
	await collection.goto();
	await seedSentGame(collection, request, 'catan', 'Catan');
	await seedSentExpansion(collection, request, catanSeafarersName);
	const collId = await acceptedAddCollId(mockBgg, seafarersBggId);
	await mockBgg.holdWrites('all');
	await collection.removeExpansion(catanSeafarersName);
	await waitUntilWriteHeld(mockBgg, 'remove', isRemoveOf(collId));
	return collId;
}

/** Releases the hold and waits until mock BGG has accepted the remove. */
export async function releaseAndWaitForRemove(mockBgg: MockBgg, collId: number): Promise<void> {
	await mockBgg.releaseWrites();
	await expect
		.poll(async () => await acceptedWriteLabels(mockBgg), { timeout: jobTimeoutMs })
		.toContain(`remove:${collId}`);
}

/** Waits until no BGG add job is enqueued or processing, so a wrongly queued add has run. */
export async function waitUntilNoAddJob(dashboard: JobsDashboardPage): Promise<void> {
	await expect.poll(() => dashboard.activeJobCount('bgg', 'BGG add'), { timeout: jobTimeoutMs }).toBe(0);
}
