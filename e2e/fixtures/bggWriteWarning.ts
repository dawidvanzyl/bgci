import { expect } from '@playwright/test';
import type { CollectionPage } from '../pages/CollectionPage';
import type { MockBgg } from './mockBgg';

const addedTail = 'but adding it to BGG failed. Add it to your BGG collection yourself.';
const removedTail = 'but removing it from BGG failed. Remove it from your BGG collection yourself.';

export const warningText = {
	gameAdd: (name: string): string => `"${name}" was added to your collection, ${addedTail}`,
	gameRemove: (name: string): string => `"${name}" was removed from your collection, ${removedTail}`,
	expansionAdd: (name: string): string => `The expansion "${name}" was added to your collection, ${addedTail}`,
	expansionRemove: (name: string): string => `The expansion "${name}" was removed from your collection, ${removedTail}`,
};

export const catanBggId = 13;
export const seafarersBggId = 325;

const tolerance = 1;

/** Adds Catan from a BGG search and waits until the card is listed. */
export async function addCatanFromBgg(collection: CollectionPage): Promise<void> {
	await collection.importFromBgg('catan', 'Catan');
	await expect(collection.card('Catan')).toBeVisible();
}

/** Adds Catan from BGG while the writer works, and waits until mock BGG has recorded the add. */
export async function seedCatanWithWriterCall(collection: CollectionPage, mockBgg: MockBgg): Promise<void> {
	await addCatanFromBgg(collection);
	await expect
		.poll(async () => (await mockBgg.writerCalls()).map((call) => `${call.action}:${call.bggId}`))
		.toEqual([`add:${catanBggId}`]);
}

/** Opens Catan's Expansions tab and adds Catan: Seafarers from the known BGG expansions. */
export async function addSeafarersToCatan(collection: CollectionPage): Promise<void> {
	await collection.openInfo('Catan');
	await collection.openExpansionsTab();
	await collection.addKnownBggExpansion('Catan: Seafarers');
}

/** Checks that the page warning sits under the header actions, above the toolbar block, and spans its width. */
export async function expectPageWarningPlacement(collection: CollectionPage): Promise<void> {
	const page = collection.pageWarning.root.page();
	const warning = await collection.box(collection.pageWarning.root);
	const header = await collection.box(collection.header);
	const toolbar = await collection.box(collection.toolbarBlock);
	const sortRow = await collection.box(page.locator('#sort-bar'));
	const grid = await collection.box(page.locator('#game-grid'));

	expect(warning.top).toBeGreaterThanOrEqual(header.bottom - tolerance);
	expect(warning.bottom).toBeLessThanOrEqual(toolbar.top + tolerance);
	expect(warning.bottom).toBeLessThanOrEqual(sortRow.top + tolerance);
	expect(warning.bottom).toBeLessThanOrEqual(grid.top + tolerance);
	expect(Math.abs(warning.width - toolbar.width)).toBeLessThanOrEqual(tolerance);
}

/** Checks that the inline warning is the first element of the Expansions tab and sits above Add Expansion. */
export async function expectInlineWarningPlacement(collection: CollectionPage): Promise<void> {
	await expect(collection.expansionsTab.locator(':scope > *').first()).toHaveId('expansion-bgg-write-warning');
	const warning = await collection.box(collection.expansionWarning.root);
	const tab = await collection.box(collection.expansionsTab);
	const heading = await collection.box(collection.expansionsTab.getByRole('heading', { name: 'Add Expansion' }));

	expect(warning.top).toBeGreaterThanOrEqual(tab.top - tolerance);
	expect(warning.bottom).toBeLessThanOrEqual(tab.bottom + tolerance);
	expect(warning.bottom).toBeLessThanOrEqual(heading.top + tolerance);
}

/** The page warning is present but hidden: it carries the hidden attribute and is not visible. */
export async function expectPageWarningHidden(collection: CollectionPage): Promise<void> {
	await expect(collection.pageWarning.root).toHaveAttribute('hidden', '');
	await expect(collection.pageWarning.root).toBeHidden();
}
