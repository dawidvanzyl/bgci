const isDev = window.location.hostname === 'localhost' && !['80', '443', ''].includes(window.location.port);
const API = isDev ? 'http://localhost:5074/api' : '/api';

// ── State ──────────────────────────────────────────────────
let allGames = [];
let deleteTargetId = null;
let bggSearchTimer = null;

// ── DOM refs ───────────────────────────────────────────────
const gameGrid         = document.getElementById('game-grid');
const emptyState       = document.getElementById('empty-state');
const filterInput      = document.getElementById('filter-input');
const bggSearchInput   = document.getElementById('bgg-search-input');
const bggResults       = document.getElementById('bgg-search-results');
const modalOverlay     = document.getElementById('modal-overlay');
const modalTitle       = document.getElementById('modal-title');
const gameForm         = document.getElementById('game-form');
const confirmOverlay   = document.getElementById('confirm-overlay');
const confirmMessage   = document.getElementById('confirm-message');

// ── Bootstrap ──────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  loadGames();
  bindEvents();
});

function bindEvents() {
  document.getElementById('btn-add-manual').addEventListener('click', () => openModal());
  document.getElementById('modal-close').addEventListener('click', closeModal);
  document.getElementById('btn-cancel').addEventListener('click', closeModal);
  gameForm.addEventListener('submit', handleFormSubmit);

  filterInput.addEventListener('input', () => renderGames(filterInput.value.trim().toLowerCase()));

  bggSearchInput.addEventListener('input', () => {
    clearTimeout(bggSearchTimer);
    const q = bggSearchInput.value.trim();
    if (q.length < 2) { hideBggResults(); return; }
    bggSearchTimer = setTimeout(() => searchBgg(q), 400);
  });

  document.addEventListener('click', (e) => {
    if (!bggResults.contains(e.target) && e.target !== bggSearchInput) hideBggResults();
  });

  document.getElementById('form-cover').addEventListener('input', e => {
    updateCoverPreview(e.target.value.trim());
  });

  document.getElementById('btn-confirm-cancel').addEventListener('click', () => {
    confirmOverlay.classList.add('hidden');
    deleteTargetId = null;
  });

  document.getElementById('btn-confirm-delete').addEventListener('click', async () => {
    if (!deleteTargetId) return;
    await deleteGame(deleteTargetId);
    confirmOverlay.classList.add('hidden');
    deleteTargetId = null;
  });
}

// ── API calls ──────────────────────────────────────────────
async function loadGames() {
  const res = await fetch(`${API}/games`);
  allGames = await res.json();
  renderGames(filterInput.value.trim().toLowerCase());
}

async function saveGame(payload, id) {
  const url  = id ? `${API}/games/${id}` : `${API}/games`;
  const method = id ? 'PUT' : 'POST';
  const res = await fetch(url, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  if (!res.ok) throw new Error(await res.text());
}

async function saveGameFromBgg(payload) {
  const res = await fetch(`${API}/games/from-bgg`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  if (!res.ok) throw new Error(await res.text());
}

async function deleteGame(id) {
  await fetch(`${API}/games/${id}`, { method: 'DELETE' });
  await loadGames();
}

async function searchBgg(q) {
  showBggLoading();
  try {
    const res = await fetch(`${API}/bgg/search?query=${encodeURIComponent(q)}`);
    const data = await res.json();
    renderBggResults(data);
  } catch {
    hideBggResults();
  }
}

async function fetchBggPreview(bggId) {
  const res = await fetch(`${API}/bgg/game/${bggId}/preview`);
  if (!res.ok) return null;
  return res.json();
}

// ── Render ─────────────────────────────────────────────────
function renderGames(filter = '') {
  const filtered = filter
    ? allGames.filter(g =>
        g.name.toLowerCase().includes(filter) ||
        (g.categories || []).some(c => c.toLowerCase().includes(filter)) ||
        (g.mechanics  || []).some(m => m.toLowerCase().includes(filter))
      )
    : allGames;

  // Remove existing cards but keep the empty state
  [...gameGrid.querySelectorAll('.game-card')].forEach(el => el.remove());

  if (filtered.length === 0) {
    emptyState.style.display = '';
    return;
  }
  emptyState.style.display = 'none';

  filtered.forEach(game => {
    gameGrid.appendChild(buildCard(game));
  });
}

function buildCard(game) {
  const card = document.createElement('div');
  card.className = 'game-card';
  card.dataset.id = game.id;

  const coverHtml = game.coverImageUrl
    ? `<img class="game-card-cover" src="${esc(game.coverImageUrl)}" alt="${esc(game.name)}" loading="lazy" />`
    : `<div class="game-card-cover-placeholder">&#127921;</div>`;

  const ratingBadge = game.bggRating
    ? `<span class="badge badge-rating">&#9733; ${Number(game.bggRating).toFixed(1)}</span>`
    : '';

  const playersBadge = game.minPlayers
    ? `<span class="badge">&#128100; ${game.minPlayers === game.maxPlayers ? game.minPlayers : `${game.minPlayers}–${game.maxPlayers}`}</span>`
    : '';

  const timeBadge = game.playTimeMinutes
    ? `<span class="badge">&#9201; ${game.playTimeMinutes} min</span>`
    : '';

  const tags = [...(game.categories || []).slice(0, 3), ...(game.mechanics || []).slice(0, 2)]
    .map(t => `<span class="tag">${esc(t)}</span>`).join('');

  card.innerHTML = `
    ${coverHtml}
    <div class="game-card-body">
      <div class="game-card-title">${esc(game.name)}</div>
      ${game.year ? `<div class="game-card-year">${game.year}</div>` : ''}
      <div class="game-card-meta">${ratingBadge}${playersBadge}${timeBadge}</div>
      ${tags ? `<div class="game-card-categories">${tags}</div>` : ''}
    </div>
    <div class="game-card-actions">
      <button class="btn btn-secondary btn-edit">Edit</button>
      <button class="btn btn-danger btn-delete">Remove</button>
    </div>
  `;

  card.querySelector('.btn-edit').addEventListener('click', () => openModal(game));
  card.querySelector('.btn-delete').addEventListener('click', () => confirmDelete(game));

  return card;
}

// ── BGG Dropdown ───────────────────────────────────────────
function renderBggResults(results) {
  bggResults.innerHTML = '';
  if (!results.length) {
    bggResults.innerHTML = '<li class="dropdown-empty">No results found.</li>';
  } else {
    results.forEach(r => {
      const li = document.createElement('li');
      li.innerHTML = `${esc(r.name)}<span class="year">${r.year ?? ''}</span>`;
      li.addEventListener('click', () => selectBggGame(r));
      bggResults.appendChild(li);
    });
  }
  bggResults.hidden = false;
}

function showBggLoading() {
  bggResults.innerHTML = '<li class="dropdown-loading">Searching BGG…</li>';
  bggResults.hidden = false;
}

function hideBggResults() {
  bggResults.hidden = true;
  bggResults.innerHTML = '';
}

async function selectBggGame(result) {
  hideBggResults();
  bggSearchInput.value = '';

  const preview = await fetchBggPreview(result.bggId);
  if (!preview) { alert('Could not load game details from BGG.'); return; }

  openModal(null, preview);
}

// ── Cover Image Preview ────────────────────────────────────
function updateCoverPreview(url) {
  const container = document.getElementById('cover-preview');
  if (!url) {
    container.textContent = '';
    container.innerHTML = '&#127921;';
    container.className = 'cover-preview-box cover-preview-empty';
    return;
  }
  const img = document.createElement('img');
  img.src = url;
  img.alt = 'Cover preview';
  img.className = 'cover-preview-box';
  img.onerror = () => {
    container.innerHTML = '&#127921;';
    container.className = 'cover-preview-box cover-preview-empty';
  };
  container.className = '';
  container.innerHTML = '';
  container.appendChild(img);
}

// ── Modal ──────────────────────────────────────────────────
function openModal(game = null, bggPreview = null) {
  gameForm.reset();
  document.getElementById('form-id').value = '';
  document.getElementById('form-bgg-id').value = '';
  updateCoverPreview('');

  if (game) {
    modalTitle.textContent = 'Edit Game';
    document.getElementById('form-id').value           = game.id;
    document.getElementById('form-bgg-id').value       = game.bggId ?? '';
    document.getElementById('form-name').value         = game.name;
    document.getElementById('form-year').value         = game.year ?? '';
    document.getElementById('form-bgg-rating').value   = game.bggRating ?? '';
    document.getElementById('form-min-players').value  = game.minPlayers ?? '';
    document.getElementById('form-max-players').value  = game.maxPlayers ?? '';
    document.getElementById('form-play-time').value    = game.playTimeMinutes ?? '';
    document.getElementById('form-cover').value        = game.coverImageUrl ?? '';
    updateCoverPreview(game.coverImageUrl ?? '');
    document.getElementById('form-categories').value   = (game.categories || []).join(', ');
    document.getElementById('form-mechanics').value    = (game.mechanics  || []).join(', ');
    document.getElementById('form-description').value  = game.description ?? '';
  } else if (bggPreview) {
    modalTitle.textContent = 'Add from BGG';
    document.getElementById('form-bgg-id').value       = bggPreview.bggId ?? '';
    document.getElementById('form-name').value         = bggPreview.name ?? '';
    document.getElementById('form-year').value         = bggPreview.year ?? '';
    document.getElementById('form-bgg-rating').value   = bggPreview.bggRating ?? '';
    document.getElementById('form-min-players').value  = bggPreview.minPlayers ?? '';
    document.getElementById('form-max-players').value  = bggPreview.maxPlayers ?? '';
    document.getElementById('form-play-time').value    = bggPreview.playTimeMinutes ?? '';
    document.getElementById('form-cover').value        = bggPreview.coverImageUrl ?? '';
    updateCoverPreview(bggPreview.coverImageUrl ?? '');
    document.getElementById('form-categories').value   = (bggPreview.categories || []).join(', ');
    document.getElementById('form-mechanics').value    = (bggPreview.mechanics  || []).join(', ');
    document.getElementById('form-description').value  = bggPreview.description ?? '';
  } else {
    modalTitle.textContent = 'Add Game Manually';
  }

  modalOverlay.classList.remove('hidden');
  document.getElementById('form-name').focus();
}

function closeModal() {
  modalOverlay.classList.add('hidden');
}

async function handleFormSubmit(e) {
  e.preventDefault();

  const name = document.getElementById('form-name').value.trim();
  if (!name) { document.getElementById('form-name').focus(); return; }

  const id    = document.getElementById('form-id').value || null;
  const bggId = parseInt(document.getElementById('form-bgg-id').value) || null;

  const splitTags = val => val.split(',').map(s => s.trim()).filter(Boolean);

  const payload = {
    name,
    year:             intOrNull('form-year'),
    description:      document.getElementById('form-description').value.trim() || null,
    minPlayers:       intOrNull('form-min-players'),
    maxPlayers:       intOrNull('form-max-players'),
    playTimeMinutes:  intOrNull('form-play-time'),
    bggRating:        floatOrNull('form-bgg-rating'),
    coverImageUrl:    document.getElementById('form-cover').value.trim() || null,
    categories:       splitTags(document.getElementById('form-categories').value),
    mechanics:        splitTags(document.getElementById('form-mechanics').value),
  };

  try {
    if (id) {
      await saveGame(payload, id);
    } else if (bggId) {
      await saveGameFromBgg({ ...payload, bggId });
    } else {
      await saveGame(payload, null);
    }
    closeModal();
    await loadGames();
  } catch (err) {
    alert(`Failed to save: ${err.message}`);
  }
}

// ── Delete Confirm ─────────────────────────────────────────
function confirmDelete(game) {
  deleteTargetId = game.id;
  confirmMessage.textContent = `Remove "${game.name}" from your collection?`;
  confirmOverlay.classList.remove('hidden');
}

// ── Helpers ────────────────────────────────────────────────
function esc(str) {
  return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function intOrNull(id) {
  const v = parseInt(document.getElementById(id).value);
  return isNaN(v) ? null : v;
}

function floatOrNull(id) {
  const v = parseFloat(document.getElementById(id).value);
  return isNaN(v) ? null : v;
}
