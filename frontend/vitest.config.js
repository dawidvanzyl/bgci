import { defineConfig } from 'vitest/config';

export default defineConfig({
	test: {
		environment: 'node',
		include: ['tests/**/*.test.js'],
		// Every UC code a frontend test uses is registered here: name is the code, description is the
		// behaviour it proves. A test that uses an unregistered tag fails the run.
		tags: [],
	},
});
