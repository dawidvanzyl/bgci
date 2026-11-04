import js from '@eslint/js';
import playwright from 'eslint-plugin-playwright';
import prettier from 'eslint-config-prettier';
import tseslint from 'typescript-eslint';

export default tseslint.config(
	{ ignores: ['node_modules/', 'playwright-report/', 'test-results/', 'mock-bgg/'] },
	js.configs.recommended,
	...tseslint.configs.recommended,
	{
		files: ['features/**/*.ts'],
		...playwright.configs['flat/recommended'],
	},
	{ rules: { 'no-empty-pattern': ['error', { allowObjectPatternsAsParameters: true }] } },
	prettier
);
