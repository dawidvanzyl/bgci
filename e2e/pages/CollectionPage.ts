import type { Locator, Page } from '@playwright/test';

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

export class CollectionPage {
	readonly emptyState: Locator;
	readonly syncButton: Locator;
	readonly cards: Locator;
	readonly cardTitles: Locator;
	readonly infoPanel: Locator;
	readonly expansionsTab: Locator;
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
		this.bggSearchInput = page.locator('#bgg-search-input');
		this.saveButton = page.locator('#btn-save');
		this.filterInput = page.locator('#filter-input');
	}

	async goto(): Promise<void> {
		await this.page.goto('/');
		await this.page.locator('body[data-ready="true"]').waitFor();
	}

	async reload(): Promise<void> {
		await this.page.reload();
		await this.page.locator('body[data-ready="true"]').waitFor();
	}

	/** Matches the card whose title is exactly the name, with or without the year suffix. */
	card(name: string): Locator {
		const exactTitle = new RegExp(`^${escapeRegExp(name)}(?:\\s*\\(\\d{4}\\))?$`);
		return this.page.locator('.game-card', {
			has: this.page.locator('.game-card-title', { hasText: exactTitle }),
		});
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
