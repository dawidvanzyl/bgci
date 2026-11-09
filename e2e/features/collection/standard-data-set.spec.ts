import { bggGame, readBggGames } from '../../fixtures/bggGames';
import { expect, test } from '../../fixtures/base';
import { CollectionPage, listFields } from '../../pages/CollectionPage';

const escapeRegExp = (text: string): string => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

test.describe('Standard data set — mock BGG serves the games the data set defines', { tag: ['@72IT20'] }, () => {
	test('S1 — a base game search result and details match its data-set entry', async ({ page }) => {
		const azul = bggGame(230802);
		expect(azul.expansionOf).toBeUndefined();
		const collection = new CollectionPage(page);
		await collection.goto();

		await collection.searchBgg(azul.name);
		const result = collection.bggResults.locator('li', { hasText: azul.name });
		await expect(result).toHaveCount(1);
		await expect(result.locator('.year')).toHaveText(String(azul.year));

		await collection.selectBggResult(azul.name);
		const expectedFields = {
			name: azul.name,
			year: String(azul.year),
			rating: String(azul.rating),
			minPlayers: String(azul.minPlayers),
			maxPlayers: String(azul.maxPlayers),
			playTime: String(azul.playTime),
			minPlayTime: String(azul.minPlayTime),
			maxPlayTime: String(azul.maxPlayTime),
			weight: String(azul.weight),
			minAge: String(azul.minAge),
			description: azul.description,
		};
		for (const [field, value] of Object.entries(expectedFields) as [keyof typeof expectedFields, string][]) {
			await expect(collection.formField(field), field).toHaveValue(value);
		}
		for (const field of listFields) {
			await expect(collection.formField(field), field).toHaveValue(azul[field].join(', '));
		}

		await collection.saveForm();
		const card = collection.card(azul.name);
		await expect(card.locator('.game-card-title')).toContainText(`(${azul.year})`);
		await expect(card.locator('.badge-rating')).toContainText(azul.rating.toFixed(1));

		await collection.openInfo(azul.name);
		await expect(collection.infoPanel.getByRole('link', { name: /View on BGG/ })).toHaveAttribute(
			'href',
			new RegExp(`boardgame/${azul.id}$`)
		);
	});

	test('S2 — a parent game offers the expansions its data-set entry lists', async ({ page }) => {
		const parent = bggGame(13);
		const expansion = bggGame(325);
		expect(expansion.expansionOf).toBe(parent.id);
		expect(parent.expansions.map((entry) => entry.id)).toContain(expansion.id);
		expect(parent.expansions).toHaveLength(1);
		const listed = parent.expansions[0];
		const collection = new CollectionPage(page);
		await collection.goto();

		await collection.searchBgg(parent.name);
		await collection.selectBggResult(parent.name);
		await collection.saveForm();
		await expect(collection.card(parent.name)).toBeVisible();
		await collection.openInfo(parent.name);
		await collection.openExpansionsTab();

		await expect(collection.knownExpansionOptions).toHaveText(
			parent.expansions.map((entry) => new RegExp(`^${escapeRegExp(entry.name)}(?: \\(\\d{4}\\))?$`))
		);
		await collection.addKnownBggExpansion(listed.name);

		await expect(collection.ownedExpansion(listed.name)).toBeVisible();
		await collection.closeModal();
		await expect(collection.card(listed.name)).toHaveCount(0);
	});

	test('S3 — mock BGG serves every data-set entry under its real id, and nothing else', async ({ mockBgg }) => {
		const games = readBggGames();
		expect(games.length).toBeGreaterThan(0);
		expect(new Set(games.map((game) => game.id)).size).toBe(games.length);

		for (const game of games) {
			const things = await mockBgg.thing(game.id);
			expect(things, `thing ${game.id}`).toHaveLength(1);
			expect(things[0], `thing ${game.id}`).toEqual({
				id: game.id,
				name: game.name,
				year: game.year,
				minPlayers: game.minPlayers,
				maxPlayers: game.maxPlayers,
				playTime: game.playTime,
				minPlayTime: game.minPlayTime,
				maxPlayTime: game.maxPlayTime,
				minAge: game.minAge,
				rating: game.rating,
				weight: game.weight,
			});
		}

		for (const game of games.filter((entry) => entry.expansionOf === undefined)) {
			const found = await mockBgg.search(game.name);
			expect(found, `search ${game.name}`).toContainEqual({ id: game.id, name: game.name });
		}

		for (const game of games.filter((entry) => entry.expansionOf !== undefined)) {
			const found = await mockBgg.search(game.name);
			expect(
				found.map((item) => item.id),
				`search ${game.name}`
			).not.toContain(game.id);
		}
	});

	test('S4 — a game that is not in the data set is not served', async ({ page, mockBgg }) => {
		const games = readBggGames();
		const term = 'zzqx';
		const unknownId = Math.max(...games.map((game) => game.id)) + 1;
		expect(games.some((game) => game.name.toLowerCase().includes(term))).toBe(false);
		expect(games.some((game) => game.id === unknownId)).toBe(false);
		const collection = new CollectionPage(page);
		await collection.goto();

		await collection.searchBgg(term);

		await expect(collection.bggResults.locator('li')).toHaveText(['No results found.']);
		expect(await mockBgg.thing(unknownId)).toEqual([]);
	});
});
