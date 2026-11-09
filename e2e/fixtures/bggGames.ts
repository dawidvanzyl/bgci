import { readFileSync } from 'node:fs';

export interface BggGameEntry {
	id: number;
	name: string;
	year: number;
	description: string;
	minPlayers: number;
	maxPlayers: number;
	playTime: number;
	minPlayTime: number;
	maxPlayTime: number;
	minAge: number;
	rating: number;
	weight: number;
	categories: string[];
	mechanics: string[];
	designers: string[];
	artists: string[];
	publishers: string[];
	subdomains: string[];
	expansions: { id: number; name: string }[];
	expansionOf?: number;
	liveWritable: boolean;
}

const dataSetPath = new URL('../../test-data/bgg-games.json', import.meta.url);

/** Reads the standard game data set from test-data/bgg-games.json. */
export function readBggGames(): BggGameEntry[] {
	return (JSON.parse(readFileSync(dataSetPath, 'utf8')) as { games: BggGameEntry[] }).games;
}

export function bggGame(id: number): BggGameEntry {
	const game = readBggGames().find((entry) => entry.id === id);
	if (!game) {
		throw new Error(`The game data set has no entry with id ${id}`);
	}
	return game;
}
