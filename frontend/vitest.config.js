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
			{ name: '90UC7', description: 'A failed BGG add resolves and yields the add warning with the BGG link' },
			{ name: '90UC8', description: 'A failed BGG remove resolves and yields the remove warning with the BGG link' },
			{ name: '90UC9', description: 'A succeeded, skipped or none outcome yields no warning' },
		],
	},
});
