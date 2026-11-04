import { describe, expect, it } from 'vitest';
import { esc } from '../src/helpers.js';

describe('esc', () => {
	it('escapes HTML special characters', () => {
		expect(esc('<b class="x">&</b>')).toBe('&lt;b class=&quot;x&quot;&gt;&amp;&lt;/b&gt;');
	});

	it('returns an empty string for null and undefined', () => {
		expect(esc(null)).toBe('');
		expect(esc(undefined)).toBe('');
	});

	it('converts numbers to strings', () => {
		expect(esc(42)).toBe('42');
	});
});
