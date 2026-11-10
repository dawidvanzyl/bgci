export function bggWriteWarning({ bggWrite, action, isExpansion, name, bggId }) {
	if (bggWrite !== 'failed') return null;

	const subject = isExpansion ? `The expansion "${name}"` : `"${name}"`;
	const text =
		action === 'add'
			? `${subject} was added to your collection, but adding it to BGG failed. Add it to your BGG collection yourself.`
			: `${subject} was removed from your collection, but removing it from BGG failed. Remove it from your BGG collection yourself.`;

	return { text, url: `https://boardgamegeek.com/boardgame/${bggId}` };
}
