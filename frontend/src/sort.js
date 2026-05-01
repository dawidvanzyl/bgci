import { SORT_FIELDS, DEFAULT_SORT, sortCriteria, setSortCriteria } from './state.js';
export { SORT_FIELDS } from './state.js';

export function saveSort() {
	localStorage.setItem('bgci-sort', JSON.stringify(sortCriteria));
}

export function sortGames(games) {
	return [...games].sort((a, b) => {
		for (const { field, dir } of sortCriteria) {
			const def = SORT_FIELDS.find(f => f.field === field);
			if (!def) continue;  // skip unknown/stale fields
			const av = a[field] ?? null;
			const bv = b[field] ?? null;
			// nulls always last
			if (av === null && bv === null) continue;
			if (av === null) return 1;
			if (bv === null) return -1;
			let cmp = 0;
			if (def.type === 'string') {
				cmp = av.localeCompare(bv);
			} else {
				cmp = av - bv;
			}
			if (cmp !== 0) return dir === 'asc' ? cmp : -cmp;
		}
		return 0;
	});
}

export function renderSortBar(onSortChanged) {
	const chipsEl  = document.getElementById('sort-chips');
	const clearBtn = document.getElementById('btn-sort-clear');
	const addBtn   = document.getElementById('btn-sort-add');

	chipsEl.innerHTML = '';
	sortCriteria.forEach(({ field, dir }) => {
		const def = SORT_FIELDS.find(f => f.field === field);
		if (!def) return;
		const chip = document.createElement('div');
		chip.className = 'sort-chip';
		chip.dataset.field = field;
		chip.innerHTML = `
			<span class="sort-chip-label">${def.label}</span>
			<button class="sort-chip-dir" title="Toggle direction" aria-label="Toggle sort direction for ${def.label}">${dir === 'asc' ? '&#8593;' : '&#8595;'}</button>
			<button class="sort-chip-remove" title="Remove" aria-label="Remove sort for ${def.label}">&times;</button>
		`;
		chipsEl.appendChild(chip);
	});

	// Show Clear only when not at default
	const isDefault = sortCriteria.length === 1 && sortCriteria[0].field === 'name' && sortCriteria[0].dir === 'asc';
	clearBtn.hidden = isDefault;

	// Hide Add when all fields are used
	addBtn.hidden = sortCriteria.length >= SORT_FIELDS.length;
}

export function toggleSortDropdown(onSortChanged) {
	const dropdown = document.getElementById('sort-add-dropdown');
	if (!dropdown.hidden) { hideSortDropdown(); return; }

	const used = new Set(sortCriteria.map(x => x.field));
	dropdown.innerHTML = '';
	SORT_FIELDS.filter(f => !used.has(f.field)).forEach(f => {
		const li = document.createElement('li');
		li.textContent = f.label;
		li.addEventListener('click', e => {
			e.stopPropagation();
			setSortCriteria([...sortCriteria, { field: f.field, dir: f.defaultDir }]);
			saveSort();
			hideSortDropdown();
			renderSortBar(onSortChanged);
			if (onSortChanged) onSortChanged();
		});
		dropdown.appendChild(li);
	});
	dropdown.hidden = false;
}

export function hideSortDropdown() {
	document.getElementById('sort-add-dropdown').hidden = true;
}
