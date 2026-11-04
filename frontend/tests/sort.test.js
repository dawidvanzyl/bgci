import { beforeEach, describe, expect, it } from 'vitest';
import { DEFAULT_SORT, setSortCriteria } from '../src/state.js';
import { sortGames } from '../src/sort.js';
import { createGame } from './factories/gameFactory.js';

const names = (list) => list.map((game) => game.name);

describe('sortGames', () => {
	beforeEach(() => setSortCriteria([...DEFAULT_SORT]));

	it('sorts strings ascending', () => {
		const games = [createGame({ name: 'Catan' }), createGame({ name: 'Azul' }), createGame({ name: 'Brass' })];

		expect(names(sortGames(games))).toEqual(['Azul', 'Brass', 'Catan']);
	});

	it('sorts numbers descending', () => {
		setSortCriteria([{ field: 'bggRating', dir: 'desc' }]);
		const games = [
			createGame({ name: 'Catan', bggRating: 7.1 }),
			createGame({ name: 'Azul', bggRating: 7.8 }),
			createGame({ name: 'Brass', bggRating: 8.6 }),
		];

		expect(names(sortGames(games))).toEqual(['Brass', 'Azul', 'Catan']);
	});

	it.each(['asc', 'desc'])('puts null values last when sorting %s', (dir) => {
		setSortCriteria([{ field: 'year', dir }]);
		const games = [createGame({ name: 'No year', year: null }), createGame({ year: 1995 }), createGame({ year: 2017 })];

		expect(names(sortGames(games)).at(-1)).toBe('No year');
	});

	it('sorts array fields by their first element and treats empty arrays as null', () => {
		setSortCriteria([{ field: 'subdomains', dir: 'asc' }]);
		const games = [
			createGame({ name: 'Azul', subdomains: [] }),
			createGame({ name: 'Brass', subdomains: ['Strategy'] }),
			createGame({ name: 'Catan', subdomains: ['Family'] }),
		];

		expect(names(sortGames(games))).toEqual(['Catan', 'Brass', 'Azul']);
	});

	it('ignores unknown fields', () => {
		setSortCriteria([
			{ field: 'nope', dir: 'asc' },
			{ field: 'name', dir: 'desc' },
		]);
		const games = [createGame({ name: 'Azul' }), createGame({ name: 'Brass' }), createGame({ name: 'Catan' })];

		expect(names(sortGames(games))).toEqual(['Catan', 'Brass', 'Azul']);
	});

	it('does not mutate the input array', () => {
		const games = [createGame({ name: 'Catan' }), createGame({ name: 'Azul' })];
		const copy = [...games];

		sortGames(games);

		expect(games).toEqual(copy);
	});
});
