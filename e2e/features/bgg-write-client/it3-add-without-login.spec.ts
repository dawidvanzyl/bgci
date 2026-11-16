import { expect, test } from '../../fixtures/base';
import { restartApi } from '../../fixtures/apiRestart';
import {
	azulBggId,
	expectPageWarningHidden,
	seedAzulWithWriterCall,
	warningText,
} from '../../fixtures/bggWriteWarning';
import { CollectionPage } from '../../pages/CollectionPage';

test.describe('BGG write client — add when the API cannot log in to BGG', { tag: ['@99IT3'] }, () => {
	test('S1 — add with a held session that BGG rejects, and no login possible', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedAzulWithWriterCall(collection, mockBgg);
		const before = await mockBgg.report();
		await mockBgg.setLoginsRejected(true);

		await collection.importFromBgg('catan', 'Catan');

		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.pageWarning.root).toBeVisible();
		await expect(collection.pageWarning.text).toHaveText(warningText.gameAdd('Catan'));
		const after = await mockBgg.report();
		expect(after.writes.map((write) => `${write.action}:${write.bggId}`)).toEqual([`add:${azulBggId}`]);
		expect(after.rejectedLogins - before.rejectedLogins).toBe(1);
	});

	test('S2 — the add is kept after a reload', async ({ page, mockBgg }) => {
		const collection = new CollectionPage(page);
		await collection.goto();
		await seedAzulWithWriterCall(collection, mockBgg);
		await mockBgg.setLoginsRejected(true);
		await collection.importFromBgg('catan', 'Catan');
		await expect(collection.pageWarning.text).toHaveText(warningText.gameAdd('Catan'));

		await collection.reload();

		await expect(collection.card('Catan')).toBeVisible();
		const writes = await mockBgg.acceptedWrites();
		expect(writes.map((write) => `${write.action}:${write.bggId}`)).toEqual([`add:${azulBggId}`]);
	});

	test('S3 — no session at all: nothing is sent to BGG but the login', async ({ page, mockBgg, baseURL }) => {
		test.setTimeout(300_000);
		await restartApi(baseURL ?? 'http://localhost:15100');
		await mockBgg.setLoginsRejected(true);
		const collection = new CollectionPage(page);
		await collection.goto();

		await collection.importFromBgg('catan', 'Catan');

		await expect(collection.card('Catan')).toBeVisible();
		await expect(collection.pageWarning.text).toHaveText(warningText.gameAdd('Catan'));
		const report = await mockBgg.report();
		expect(report.writes).toEqual([]);
		expect(report.rejectedLogins).toBe(1);
		expect(report.rejectedWrites).toBe(0);
	});

	test('S4 — the API recovers when logins work again', async ({ page, mockBgg }) => {
		await mockBgg.setLoginsRejected(true);
		const collection = new CollectionPage(page);
		await collection.goto();
		await collection.importFromBgg('catan', 'Catan');
		await expect(collection.pageWarning.text).toHaveText(warningText.gameAdd('Catan'));
		await collection.pageWarning.dismiss();
		await expectPageWarningHidden(collection);
		await mockBgg.setLoginsRejected(false);

		await collection.importFromBgg('azul', 'Azul');

		await expect(collection.card('Azul')).toBeVisible();
		await expect
			.poll(async () => (await mockBgg.acceptedWrites()).map((write) => `${write.action}:${write.bggId}`))
			.toEqual([`add:${azulBggId}`]);
		await expectPageWarningHidden(collection);
	});
});
