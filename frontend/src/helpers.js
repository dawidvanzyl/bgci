// ── Loading / error helpers ────────────────────────────────
const SPINNER_SVG = '<svg class="btn-spinner" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round"><path d="M12 2a10 10 0 0 1 10 10" /></svg>';

export function setButtonLoading(btn, label) {
	btn.disabled = true;
	btn.dataset.originalText = btn.textContent;
	btn.innerHTML = `${SPINNER_SVG}${label}`;
}

export function clearButtonLoading(btn) {
	btn.disabled = false;
	btn.textContent = btn.dataset.originalText;
}

export function showInlineError(anchorEl, message) {
	const existing = anchorEl.parentElement.querySelector('.form-error');
	if (existing) existing.remove();
	const el = document.createElement('p');
	el.className = 'form-error';
	el.textContent = message;
	anchorEl.parentElement.insertBefore(el, anchorEl);
	setTimeout(() => el.remove(), 5000);
}

export function showSyncError(message) {
	const syncBtn = document.getElementById('btn-sync-bgg');
	const existing = syncBtn.parentElement.querySelector('.sync-error');
	if (existing) existing.remove();
	const el = document.createElement('span');
	el.className = 'sync-error';
	el.textContent = message;
	el.style.cssText = 'color:var(--danger);font-size:13px;';
	syncBtn.parentElement.insertBefore(el, syncBtn);
	setTimeout(() => el.remove(), 5000);
}

// ── Value helpers ──────────────────────────────────────────
export function esc(str) {
	return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

export function intOrNull(id) {
	const v = parseInt(document.getElementById(id).value);
	return isNaN(v) ? null : v;
}

export function floatOrNull(id) {
	const v = parseFloat(document.getElementById(id).value);
	return isNaN(v) ? null : v;
}
