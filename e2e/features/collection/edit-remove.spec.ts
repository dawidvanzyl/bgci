import { expect, test } from '../../fixtures/base';
import { expectEditFormValues } from '../../fixtures/formValues';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('Edit game — edits persist after a reload', { tag: ['@61IT3'] }, () => {
	test('S1 — edited details and a reordered list persist', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({
			name: 'Draft Original',
			year: '2001',
			minAge: '8',
			mechanics: 'Set Collection, Dice Rolling, Bluffing',
		});

		await collection.openEdit('Draft Original');
		await collection.fillForm({
			name: 'Draft Revised',
			year: '2015',
			minAge: '14',
			mechanics: 'Bluffing, Set Collection',
		});
		await collection.saveForm();
		await collection.reload();

		await expect(collection.card('Draft Revised')).toBeVisible();
		await expect(collection.card('Draft Original')).toHaveCount(0);
		await expectEditFormValues(collection, 'Draft Revised', {
			year: '2015',
			minAge: '14',
			mechanics: 'Bluffing, Set Collection',
		});
	});

	test('S2 — editing one game does not change another', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Edit Target', categories: 'Racing' });
		await collection.addGame({ name: 'Bystander', categories: 'Trivia' });

		await collection.openEdit('Edit Target');
		await collection.fillForm({ categories: 'Racing, Pirates' });
		await collection.saveForm();
		await collection.reload();

		await expect(collection.cards).toHaveCount(2);
		await expectEditFormValues(collection, 'Edit Target', { categories: 'Racing, Pirates' });
		await expectEditFormValues(collection, 'Bystander', { categories: 'Trivia' });
	});
});

test.describe('Remove game — removals persist after a reload', { tag: ['@61IT4'] }, () => {
	test('S1 — a removed game stays removed after reload, and others remain', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Keep Me Game' });
		await collection.addGame({ name: 'Remove Me Game' });

		await collection.remove('Remove Me Game');
		await expect(collection.card('Remove Me Game')).toHaveCount(0);
		await collection.reload();

		await expect(collection.card('Remove Me Game')).toHaveCount(0);
		await expect(collection.card('Keep Me Game')).toBeVisible();
	});

	test('S2 — removing the only game shows the empty state after reload', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Lonely Game' });

		await collection.remove('Lonely Game');
		await expect(collection.emptyState).toBeVisible();
		await collection.reload();

		await expect(collection.emptyState).toBeVisible();
		await expect(collection.cards).toHaveCount(0);
	});
});
