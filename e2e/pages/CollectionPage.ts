import { expect, type Locator, type Page } from '@playwright/test';

export interface GameDetails {
	name: string;
	year?: string;
	rating?: string;
	minPlayers?: string;
	maxPlayers?: string;
	bestPlayersMin?: string;
	bestPlayersMax?: string;
	playTime?: string;
	minPlayTime?: string;
	maxPlayTime?: string;
	weight?: string;
	minAge?: string;
	cover?: string;
	description?: string;
	categories?: string;
	mechanics?: string;
	designers?: string;
	artists?: string;
	publishers?: string;
	subdomains?: string;
}

export type FormField = keyof GameDetails;

const fieldIds: Record<FormField, string> = {
	name: '#form-name',
	year: '#form-year',
	rating: '#form-bgg-rating',
	minPlayers: '#form-min-players',
	maxPlayers: '#form-max-players',
	bestPlayersMin: '#form-best-player-count-min',
	bestPlayersMax: '#form-best-player-count-max',
	playTime: '#form-play-time',
	minPlayTime: '#form-min-play-time',
	maxPlayTime: '#form-max-play-time',
	weight: '#form-bgg-weight',
	minAge: '#form-min-age',
	cover: '#form-cover',
	description: '#form-description',
	categories: '#form-categories',
	mechanics: '#form-mechanics',
	designers: '#form-designers',
	artists: '#form-artists',
	publishers: '#form-publishers',
	subdomains: '#form-subdomains',
};

export const listFields = ['categories', 'mechanics', 'designers', 'artists', 'publishers', 'subdomains'] as const;

const escapeRegExp = (text: string): string => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

export interface Box {
	top: number;
	bottom: number;
	left: number;
	right: number;
	width: number;
}

export type ViewMode = 'Large icons' | 'Medium icons' | 'Small icons' | 'List' | 'Details';

export class CollectionPage {
	readonly emptyState: Locator;
	readonly syncButton: Locator;
	readonly cards: Locator;
	readonly cardTitles: Locator;
	readonly infoPanel: Locator;
	readonly expansionsTab: Locator;
	readonly bggResults: Locator;
	readonly knownExpansionOptions: Locator;
	readonly statusBanner: Locator;
	readonly header: Locator;
	readonly toolbarBlock: Locator;
	private readonly bggSearchInput: Locator;
	private readonly saveButton: Locator;
	private readonly filterInput: Locator;

	constructor(private readonly page: Page) {
		this.emptyState = page.locator('#empty-state');
		this.syncButton = page.locator('#btn-sync-bgg');
		this.cards = page.locator('.game-card');
		this.cardTitles = page.locator('.game-card-title');
		this.infoPanel = page.locator('#game-info-panel');
		this.expansionsTab = page.locator('#expansions-tab-content');
		this.bggResults = page.locator('#bgg-search-results');
		this.knownExpansionOptions = page.locator('.exp-known-select option:not([value=""])');
		this.statusBanner = page.locator('#bgg-status-banner');
		this.header = page.locator('header');
		this.toolbarBlock = page.locator('.sticky-controls');
		this.bggSearchInput = page.locator('#bgg-search-input');
		this.saveButton = page.locator('#btn-save');
		this.filterInput = page.locator('#filter-input');
	}

	async goto(): Promise<void> {
		await this.page.goto('/');
		await this.page.locator('body[data-ready="true"]').waitFor();
	}

	/** The BGG write warning and every alert are absent from the page. The warning no longer exists. */
	async expectNoWriteWarning(): Promise<void> {
		await expect(this.page.locator('#bgg-write-warning, #expansion-bgg-write-warning, .bgg-write-warning')).toHaveCount(
			0
		);
		await expect(this.page.getByRole('alert')).toHaveCount(0);
	}

	async reload(): Promise<void> {
		await this.page.reload();
		await this.page.locator('body[data-ready="true"]').waitFor();
	}

	/** Matches the card whose title is exactly the name, with or without the year suffix. */
	card(name: string): Locator {
		const exactTitle = new RegExp(`^${escapeRegExp(name)}(?:\\s*\\(\\d{4}\\))?$`);
		return this.page.locator('.game-card', {
			has: this.page.locator('.game-card-title, .list-row-title', { hasText: exactTitle }),
		});
	}

	/** The BGG badge of the game, in any view mode. */
	bggBadge(name: string): Locator {
		return this.card(name).getByTestId('bgg-badge');
	}

	/** The status mark inside the BGG badge of the game. It is absent for a game whose BGG add is done. */
	bggStatusMark(name: string): Locator {
		return this.bggBadge(name).getByTestId('bgg-status-mark');
	}

	/** Chooses a view mode through its button. */
	async setView(mode: ViewMode): Promise<void> {
		await this.page.getByRole('button', { name: mode, exact: true }).click();
	}

	/** The BGG badge on the owned expansion row. */
	ownedExpansionBadge(name: string): Locator {
		return this.ownedExpansionRow(name).getByTestId('bgg-badge');
	}

	ownedExpansionStatusMark(name: string): Locator {
		return this.ownedExpansionBadge(name).getByTestId('bgg-status-mark');
	}

	ownedExpansionRow(name: string): Locator {
		return this.expansionsTab.locator('.exp-owned-item', {
			has: this.page.locator('.exp-owned-name', { hasText: name }),
		});
	}

	/** Reloads the page until the condition holds. */
	async reloadUntil(condition: () => Promise<boolean>, message: string): Promise<void> {
		await expect
			.poll(
				async () => {
					await this.reload();
					return condition();
				},
				{ message, timeout: 30_000 }
			)
			.toBe(true);
	}

	expansionBadge(name: string): Locator {
		return this.card(name).locator('.badge-expansions').first();
	}

	async addManually(name: string): Promise<void> {
		await this.page.locator('#btn-add-manual').click();
		await this.page.locator('#form-name').fill(name);
		await this.saveButton.click();
	}

	/** Adds a game through the manual form, with every detail given, and waits until it is listed. */
	async addGame(details: GameDetails): Promise<void> {
		await this.page.locator('#btn-add-manual').click();
		await this.fillForm(details);
		await this.saveButton.click();
		await this.page.locator('#modal-overlay.hidden').waitFor({ state: 'attached' });
		await this.card(details.name).waitFor();
	}

	async fillForm(details: Partial<GameDetails>): Promise<void> {
		for (const [field, value] of Object.entries(details) as [FormField, string][]) {
			await this.page.locator(fieldIds[field]).fill(value);
		}
	}

	formField(field: FormField): Locator {
		return this.page.locator(fieldIds[field]);
	}

	async openEdit(name: string): Promise<void> {
		await this.card(name).locator('.btn-edit').click();
		await this.page.locator('#game-form').waitFor();
	}

	async openInfo(name: string): Promise<void> {
		await this.card(name).locator('.btn-info').click();
		await this.infoPanel.waitFor();
	}

	async saveForm(): Promise<void> {
		await this.saveButton.click();
		await this.page.locator('#modal-overlay.hidden').waitFor({ state: 'attached' });
	}

	async closeModal(): Promise<void> {
		await this.page.locator('#modal-close').click();
		await this.page.locator('#modal-overlay.hidden').waitFor({ state: 'attached' });
	}

	async openExpansionsTab(): Promise<void> {
		await this.page.locator('.modal-tab-btn[data-tab="expansions"]').click();
		await this.expansionsTab.waitFor();
	}

	async addManualExpansion(name: string): Promise<void> {
		await this.expansionsTab.getByPlaceholder('Expansion name…').fill(name);
		await this.expansionsTab.locator('.exp-manual-row').getByRole('button', { name: 'Add' }).click();
		await this.expansionsTab.locator('.exp-owned-name', { hasText: name }).waitFor();
	}

	async addKnownBggExpansion(name: string): Promise<void> {
		const select = this.expansionsTab.locator('.exp-known-select');
		await this.page.locator('.exp-known-select:not([disabled])').waitFor();
		const value = await select.locator('option', { hasText: name }).getAttribute('value');
		await select.selectOption(value ?? '');
		await this.expansionsTab.locator('.exp-known-row').getByRole('button', { name: 'Add' }).click();
		await this.expansionsTab.locator('.exp-owned-name', { hasText: name }).waitFor();
	}

	ownedExpansion(name: string): Locator {
		return this.expansionsTab.locator('.exp-owned-name', { hasText: name });
	}

	async searchBgg(query: string): Promise<void> {
		await this.bggSearchInput.fill(query);
	}

	/** Picks a search result and waits until the pre-filled form is open. */
	async selectBggResult(name: string): Promise<void> {
		await this.bggResults.locator('li', { hasText: name }).click();
		await this.page.locator('#game-form').waitFor();
	}

	/** Searches BGG, picks the result, and saves the pre-filled form. */
	async importFromBgg(query: string, resultName: string): Promise<void> {
		await this.bggSearchInput.fill(query);
		await this.page.locator('#bgg-search-results li', { hasText: resultName }).click();
		await this.saveButton.click();
	}

	async clickSyncBgg(): Promise<void> {
		await this.syncButton.click();
	}

	async sync(): Promise<void> {
		const finished = this.page.waitForResponse((response) => response.url().endsWith('/api/sync/bgg'));
		await this.syncButton.click();
		await finished;
		await this.page.locator('#btn-sync-bgg:not([disabled])').waitFor();
	}

	async remove(name: string): Promise<void> {
		await this.card(name).locator('.btn-delete').click();
		await this.page.locator('#btn-confirm-delete').click();
	}

	async removeExpansion(name: string): Promise<void> {
		await this.expansionsTab.locator('.exp-owned-item', { hasText: name }).locator('.exp-remove-btn').click();
	}

	/** Reads the bounding box of an element that is on the page. */
	async box(locator: Locator): Promise<Box> {
		const box = await locator.boundingBox();
		if (!box) {
			throw new Error('The element has no bounding box');
		}
		return { top: box.y, bottom: box.y + box.height, left: box.x, right: box.x + box.width, width: box.width };
	}

	async filter(text: string): Promise<void> {
		await this.filterInput.fill(text);
	}

	async clearFilter(): Promise<void> {
		await this.filterInput.fill('');
	}

	async addSort(label: string): Promise<void> {
		await this.page.locator('#btn-sort-add').click();
		await this.page.locator('#sort-add-dropdown li', { hasText: new RegExp(`^${escapeRegExp(label)}$`) }).click();
	}

	async removeSort(label: string): Promise<void> {
		await this.page.locator(`.sort-chip-remove[aria-label="Remove sort for ${label}"]`).click();
	}

	async toggleSortDirection(label: string): Promise<void> {
		await this.page.locator(`.sort-chip-dir[aria-label="Toggle sort direction for ${label}"]`).click();
	}
}
