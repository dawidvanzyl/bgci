import { expect, test } from '../../fixtures/base';
import {
	addCatanFromBgg,
	addSeafarersToCatan,
	catanBggId,
	seafarersBggId,
	seedCatanWithWriterCall,
} from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write warning — the Open on BGG link', { tag: ['@72IT9'] }, () => {
	test('S1 — game warning link', async ({ page, mockBgg }) => {
		await mockBgg.setWriterFailing(true);
		const collection = new CollectionPage(page);
		await collection.goto();
		await addCatanFromBgg(collection);
		await expect(collection.pageWarning.root).toBeVisible();

		const link = collection.pageWarning.link;

		await expect(link).toHaveAttribute('href', `https://boardgamegeek.com/boardgame/${catanBggId}`);
		await expect(link).toHaveAttribute('target', '_blank');
		await expect(link).toHaveAttribute('rel', /noopener/);
		const badgeHref = await collection.card('Catan').locator('.bgg-badge').getAttribute('href');
		await expect(link).toHaveAttribute('href', badgeHref ?? '');
	});

	test('S2 — expansion warning link', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedCatanWithWriterCall(collection, mockBgg);
		await mockBgg.setWriterFailing(true);
		await addSeafarersToCatan(collection);
		await expect(collection.expansionWarning.root).toBeVisible();

		const link = collection.expansionWarning.link;

		await expect(link).toHaveAttribute('href', `https://boardgamegeek.com/boardgame/${seafarersBggId}`);
		await expect(link).toHaveAttribute('target', '_blank');
		await expect(link).toHaveAttribute('rel', /noopener/);
	});
});
