let nextId = 1;

/**
 * Builds a valid game object as the API returns it. State only the fields that matter to the
 * behaviour under test.
 */
export function createGame(overrides = {}) {
	const id = nextId++;
	return {
		id: `game-${id}`,
		name: `Game ${id}`,
		year: 2000,
		bggRating: 7.0,
		bggWeight: 2.5,
		minPlayers: 2,
		maxPlayers: 4,
		playTimeMinutes: 60,
		minAge: 10,
		categories: [],
		mechanics: [],
		subdomains: [],
		publishers: [],
		...overrides,
	};
}
