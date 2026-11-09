const pageWarning = document.getElementById('bgg-write-warning');

export function buildWarningParts(warning, { onDismiss }) {
	const text = document.createElement('span');
	text.className = 'bgg-write-warning-text';
	text.textContent = warning.text;

	const link = document.createElement('a');
	link.className = 'bgg-write-warning-link';
	link.textContent = 'Open on BGG';
	link.href = warning.url;
	link.target = '_blank';
	link.rel = 'noopener';

	const dismiss = document.createElement('button');
	dismiss.type = 'button';
	dismiss.className = 'btn-icon bgg-write-warning-close';
	dismiss.textContent = '×';
	dismiss.title = 'Dismiss warning';
	dismiss.setAttribute('aria-label', 'Dismiss warning');
	dismiss.addEventListener('click', onDismiss);

	return [text, link, dismiss];
}

export function showPageWarning(warning) {
	if (!warning) return;
	pageWarning.replaceChildren(...buildWarningParts(warning, { onDismiss: dismissPageWarning }));
	pageWarning.hidden = false;
}

function dismissPageWarning() {
	pageWarning.hidden = true;
	pageWarning.replaceChildren();
}
