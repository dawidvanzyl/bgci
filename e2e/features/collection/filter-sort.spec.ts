import { expect, test } from '../../fixtures/base';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Filter — user filters the collection by name', { tag: ['@61IT11'] }, () => {
	test('S1 — filter by part of a name', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Volcano Run', categories: 'Plain' });
		await collection.addGame({ name: 'Glacier Hop', categories: 'Plain' });
		await collection.addGame({ name: 'Volcano Rescue', categories: 'Plain' });
		await collection.reload();

		await collection.filter('volcano');

		await expect(collection.cards).toHaveCount(2);
		await expect(collection.card('Volcano Run')).toBeVisible();
		await expect(collection.card('Volcano Rescue')).toBeVisible();
		await expect(collection.card('Glacier Hop')).toHaveCount(0);

		await collection.clearFilter();
		await expect(collection.cards).toHaveCount(3);
	});
});

test.describe('Filter — user filters the collection by category', { tag: ['@61IT12'] }, () => {
	test('S1 — filter by a category in first and non-first position', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Alpha Table', categories: 'Pirates' });
		await collection.addGame({ name: 'Beta Table', categories: 'Racing' });
		await collection.addGame({ name: 'Gamma Table', categories: 'Racing, Pirates' });
		await collection.reload();

		await collection.filter('pirates');

		await expect(collection.cards).toHaveCount(2);
		await expect(collection.card('Alpha Table')).toBeVisible();
		await expect(collection.card('Gamma Table')).toBeVisible();
		await expect(collection.card('Beta Table')).toHaveCount(0);
	});

	test('S2 — a category value that contains a pipe is matched as one value', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Pipe Table', categories: 'Sci|Fi' });
		await collection.addGame({ name: 'Other Table', categories: 'Western' });
		await collection.reload();

		await collection.filter('sci|fi');

		await expect(collection.cards).toHaveCount(1);
		await expect(collection.card('Pipe Table')).toBeVisible();
		await expect(collection.card('Other Table')).toHaveCount(0);
	});
});

test.describe('Filter — user filters the collection by mechanic', { tag: ['@61IT13'] }, () => {
	test('S1 — filter by a mechanic', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'North Board', mechanics: 'Deck Building' });
		await collection.addGame({ name: 'South Board', mechanics: 'Area Control, Deck Building' });
		await collection.addGame({ name: 'East Board', mechanics: 'Area Control' });
		await collection.reload();

		await collection.filter('deck building');

		await expect(collection.cards).toHaveCount(2);
		await expect(collection.card('North Board')).toBeVisible();
		await expect(collection.card('South Board')).toBeVisible();
		await expect(collection.card('East Board')).toHaveCount(0);
	});
});

test.describe('Sort — user sorts the collection by year', { tag: ['@61IT14'] }, () => {
	test('S1 — sort by year, ascending and descending', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Aardvark Era', year: '2020' });
		await collection.addGame({ name: 'Mid Era', year: '1999' });
		await collection.addGame({ name: 'Zebra Era', year: '2005' });
		await collection.reload();

		await collection.addSort('Year');
		await collection.removeSort('Name');
		await expect(collection.cardTitles).toHaveText(['Mid Era (1999)', 'Zebra Era (2005)', 'Aardvark Era (2020)']);

		await collection.toggleSortDirection('Year');
		await expect(collection.cardTitles).toHaveText(['Aardvark Era (2020)', 'Zebra Era (2005)', 'Mid Era (1999)']);

		await collection.reload();
		await expect(collection.cardTitles).toHaveText(['Aardvark Era (2020)', 'Zebra Era (2005)', 'Mid Era (1999)']);
	});
});

test.describe('Sort — user sorts the collection by a list field', { tag: ['@61IT15'] }, () => {
	test('S1 — sort by the Subdomain list field uses each game first stored value', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({
			name: 'Game One',
			designers: 'Dee Zed',
			subdomains: 'Thematic Games, Abstract Games',
		});
		await collection.addGame({ name: 'Game Two', designers: 'Ann Bee', subdomains: 'Family Games' });
		await collection.addGame({
			name: 'Game Three',
			designers: 'Cal Em',
			subdomains: 'Abstract Games, Wargames',
		});
		await collection.reload();

		await collection.addSort('Subdomain');
		await collection.removeSort('Name');

		await expect(collection.cardTitles).toHaveText(['Game Three', 'Game Two', 'Game One']);
	});
});

test.describe('Filter — a filter that matches nothing', { tag: ['@61IT16'] }, () => {
	test('S1 — a filter that matches nothing', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Red Box', categories: 'Farming' });
		await collection.addGame({ name: 'Blue Box', categories: 'Space' });
		await collection.reload();

		await collection.filter('underwater');

		await expect(collection.cards).toHaveCount(0);
		await expect(collection.emptyState).toBeVisible();

		await collection.clearFilter();
		await expect(collection.card('Red Box')).toBeVisible();
		await expect(collection.card('Blue Box')).toBeVisible();
	});
});
