import js from '@eslint/js';
import globals from 'globals';
import prettier from 'eslint-config-prettier';

export default [
	{ ignores: ['dist/', 'node_modules/', 'public/config.js'] },
	js.configs.recommended,
	{
		files: ['src/**/*.js'],
		languageOptions: {
			ecmaVersion: 2022,
			sourceType: 'module',
			globals: { ...globals.browser },
		},
		rules: {
			'no-var': 'error',
			'prefer-const': 'error',
			'no-unused-vars': ['error', { args: 'none' }],
		},
	},
	{
		files: ['*.js', 'tests/**/*.js'],
		languageOptions: {
			ecmaVersion: 2022,
			sourceType: 'module',
			globals: { ...globals.node },
		},
	},
	prettier,
];
