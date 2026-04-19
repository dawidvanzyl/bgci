'use strict';

const globals = require('globals');

module.exports = [
	{
		languageOptions: {
			ecmaVersion: 2022,
			sourceType: 'commonjs',
			globals: {
				...globals.node,
				...globals.browser,
			},
		},
		rules: {
			'strict': ['error', 'global'],
			'no-unused-vars': 'error',
			'no-undef': 'error',
			'no-console': 'off',
		},
	},
];
