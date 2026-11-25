import { defineConfig } from 'vitest/config';

export default defineConfig({
	test: {
		environment: 'node',
		include: ['tests/**/*.test.js'],
		// Every UC code a frontend test uses is registered here: name is the code, description is the
		// behaviour it proves. A test that uses an unregistered tag fails the run.
		tags: [
			{ name: '55UC1', description: 'Sync request sends POST to the sync endpoint and resolves' },
			{ name: '55UC2', description: 'Sync request rejects with the response text on a server error' },
			{ name: '111UC17', description: 'The BGG badge shows the right sentence, glyph and word for each status' },
			{ name: '111UC18', description: 'A game added by hand has no BGG badge and no status' },
			{ name: '111UC19', description: 'An add resolves to the id and status, and a remove resolves with no value' },
			{
				name: '112UC7',
				description: 'A refused add from BGG becomes a refusal error with the game name and the exact message',
			},
		],
	},
});
