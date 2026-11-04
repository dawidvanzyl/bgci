import type { Locator, Page } from '@playwright/test';

export class CollectionPage {
	readonly emptyState: Locator;
	private readonly bggSearchInput: Locator;
	private readonly saveButton: Locator;

	constructor(private readonly page: Page) {
		this.emptyState = page.locator('#empty-state');
		this.bggSearchInput = page.locator('#bgg-search-input');
		this.saveButton = page.locator('#btn-save');
	}

	async goto(): Promise<void> {
		await this.page.goto('/');
		await this.page.locator('body[data-ready="true"]').waitFor();
	}

	card(name: string): Locator {
		return this.page.locator('.game-card', { has: this.page.locator('.game-card-title', { hasText: name }) });
	}

	async addManually(name: string): Promise<void> {
		await this.page.locator('#btn-add-manual').click();
		await this.page.locator('#form-name').fill(name);
		await this.saveButton.click();
	}

	/** Searches BGG, picks the result, and saves the pre-filled form. */
	async importFromBgg(query: string, resultName: string): Promise<void> {
		await this.bggSearchInput.fill(query);
		await this.page.locator('#bgg-search-results li', { hasText: resultName }).click();
		await this.saveButton.click();
	}

	async remove(name: string): Promise<void> {
		await this.card(name).locator('.btn-delete').click();
		await this.page.locator('#btn-confirm-delete').click();
	}
}
