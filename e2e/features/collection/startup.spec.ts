import { expect, test } from '../../fixtures/base';
import { restartApi } from '../../fixtures/apiRestart';
import { expectEditFormValues } from '../../fixtures/formValues';
import { CollectionPage, listFields } from '../../pages/CollectionPage';

test.describe('Start-up — the app runs against a database it prepares itself', { tag: ['@61IT17'] }, () => {
	test('S1 — the app on the freshly started QA stack shows an empty collection', async ({ page, request }) => {
		const failedResponses: string[] = [];
		page.on('response', (response) => {
			if (response.url().includes('/api/') && response.status() >= 400) {
				failedResponses.push(`${response.status()} ${response.url()}`);
			}
		});

		const response = await request.get('/api/games');
		expect(response.ok()).toBe(true);
		expect(await response.json()).toEqual([]);

		const collection = new CollectionPage(page);
		await collection.goto();

		await expect(collection.emptyState).toBeVisible();
		await expect(collection.cards).toHaveCount(0);
		expect(failedResponses).toEqual([]);
	});

	test('S2 — the schema the API created accepts a write', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();

		await collection.addGame({ name: 'First Light Game' });
		await collection.reload();

		await expect(collection.card('First Light Game')).toBeVisible();
	});
});

test.describe('Start-up — the collection survives an API restart', { tag: ['@61IT18'] }, () => {
	test('S1 — games and lists survive an API restart', async ({ page, baseURL }) => {
		test.setTimeout(300_000);
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({
			name: 'Restart Alpha',
			categories: 'Zinc, Iron',
			mechanics: 'Trading|Barter',
			subdomains: 'Strategy Games',
		});
		await collection.addGame({ name: 'Restart Beta' });

		await restartApi(baseURL ?? 'http://localhost:15100');
		await collection.reload();

		await expect(collection.card('Restart Alpha')).toBeVisible();
		await expect(collection.card('Restart Beta')).toBeVisible();
		await expectEditFormValues(collection, 'Restart Alpha', {
			categories: 'Zinc, Iron',
			mechanics: 'Trading|Barter',
			subdomains: 'Strategy Games',
		});
		await collection.openEdit('Restart Beta');
		for (const field of listFields) {
			await expect(collection.formField(field), field).toHaveValue('');
		}
	});
});
