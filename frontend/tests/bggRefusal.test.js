import { describe, expect, it } from 'vitest';
import { BggAddRefusedError, addErrorMessage, refusedAddMessage } from '../src/bggRefusal.js';

describe('refusedAddMessage', () => {
	it('gives the base game sentence with the name in quotes', { tags: ['112UC7'] }, () => {
		expect(refusedAddMessage('Wingspan', { expansion: false })).toBe(
			`"Wingspan" can't be added yet. Removing it from your BGG collection isn't finished. Try again later.`
		);
	});

	it('gives the expansion sentence', { tags: ['112UC7'] }, () => {
		expect(refusedAddMessage('Wingspan: Oceania Expansion', { expansion: true })).toBe(
			`The expansion "Wingspan: Oceania Expansion" can't be added yet. Removing it from your BGG collection isn't finished. Try again later.`
		);
	});
});

describe('addErrorMessage', () => {
	it('maps a refusal to the expansion sentence', { tags: ['112UC7'] }, () => {
		const message = addErrorMessage(new BggAddRefusedError('Catan: Seafarers'), { expansion: true });

		expect(message).toBe(refusedAddMessage('Catan: Seafarers', { expansion: true }));
	});

	it('keeps the message of any other error', { tags: ['112UC7'] }, () => {
		expect(addErrorMessage(new Error('Failed to fetch BGG data for expansion.'), { expansion: true })).toBe(
			'Failed to fetch BGG data for expansion.'
		);
	});
});
