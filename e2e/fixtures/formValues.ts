import { expect } from './base';
import type { CollectionPage, GameDetails } from '../pages/CollectionPage';

/** Opens the edit form of a game, asserts each given field holds exactly the value, and closes the form. */
export async function expectEditFormValues(
	collection: CollectionPage,
	name: string,
	expected: Partial<GameDetails>
): Promise<void> {
	await collection.openEdit(name);
	for (const [field, value] of Object.entries(expected) as [keyof GameDetails, string][]) {
		await expect(collection.formField(field), `${name}: ${field}`).toHaveValue(value);
	}
	await collection.closeModal();
}
