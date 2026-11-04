import { expect, test } from '../../fixtures/base';
import { expectEditFormValues } from '../../fixtures/formValues';
import { CollectionPage, listFields, type GameDetails } from '../../pages/CollectionPage';

const quillmaker: GameDetails = {
	name: 'Quillmaker Guild',
	year: '2019',
	rating: '7.5',
	minPlayers: '2',
	maxPlayers: '5',
	bestPlayersMin: '3',
	bestPlayersMax: '4',
	playTime: '75',
	minPlayTime: '45',
	maxPlayTime: '90',
	weight: '2.75',
	minAge: '12',
	cover: 'https://example.test/quill.png',
	description: 'Ink and parchment trading.',
	categories: 'Zymurgy, Antiques, Medieval',
	mechanics: 'Worker Placement, Hand|Management, Auction',
	designers: 'Yara Voss, Ben Ode',
	artists: 'Tomas Rek, Ann Lu',
	publishers: 'Wren Press, Ash House',
	subdomains: 'Strategy Games, Family Games',
};

test.describe('Add game — user adds a game manually with every detail', { tag: ['@61IT1'] }, () => {
	test('S1 — every detail and every list order survive a reload', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await expect(collection.emptyState).toBeVisible();

		await collection.addGame(quillmaker);
		await collection.reload();

		await expect(collection.cards).toHaveCount(1);
		await expect(collection.card('Quillmaker Guild').locator('.game-card-title')).toContainText('(2019)');
		await expectEditFormValues(collection, 'Quillmaker Guild', quillmaker);
	});

	test('S2 — two games keep their own lists', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Ferry Lines', categories: 'Transport, Puzzle' });
		await collection.addGame({ name: 'Moss Garden', categories: 'Nature' });

		await collection.reload();

		await expect(collection.cards).toHaveCount(2);
		await expectEditFormValues(collection, 'Ferry Lines', { categories: 'Transport, Puzzle' });
		await expectEditFormValues(collection, 'Moss Garden', { categories: 'Nature' });
	});
});

test.describe('Add game — user adds a game manually without lists', { tag: ['@61IT2'] }, () => {
	test('S1 — a game with only a name shows no list entries', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();

		await collection.addGame({ name: 'Bare Bones Game' });
		await collection.reload();

		await expect(collection.card('Bare Bones Game')).toBeVisible();
		await expect(collection.card('Bare Bones Game').locator('.tag')).toHaveCount(0);
		await collection.openEdit('Bare Bones Game');
		for (const field of listFields) {
			await expect(collection.formField(field), field).toHaveValue('');
		}
	});

	test('S2 — clearing every list on edit leaves no entries', async ({ page }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.addGame({ name: 'Clearout Game', categories: 'Cards', mechanics: 'Drafting' });

		await collection.openEdit('Clearout Game');
		await collection.fillForm({
			categories: '',
			mechanics: '',
			designers: '',
			artists: '',
			publishers: '',
			subdomains: '',
		});
		await collection.saveForm();
		await collection.reload();

		await expect(collection.card('Clearout Game').locator('.tag')).toHaveCount(0);
		await collection.openEdit('Clearout Game');
		for (const field of listFields) {
			await expect(collection.formField(field), field).toHaveValue('');
		}
	});
});
