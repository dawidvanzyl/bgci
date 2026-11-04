const escapeXml = (value) =>
	String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

const link = (type, value, extra = '') => `<link type="${type}" id="0" value="${escapeXml(value)}"${extra}/>`;

export function searchXml(matches) {
	const items = matches
		.map(
			(g) =>
				`<item type="boardgame" id="${g.id}"><name type="primary" value="${escapeXml(g.name)}"/>` +
				`<yearpublished value="${g.year}"/></item>`
		)
		.join('');
	return `<?xml version="1.0" encoding="utf-8"?><items total="${matches.length}">${items}</items>`;
}

export function thingXml(game, imageBaseUrl) {
	const links = [
		...game.categories.map((c) => link('boardgamecategory', c)),
		...game.mechanics.map((m) => link('boardgamemechanic', m)),
		...game.designers.map((d) => link('boardgamedesigner', d)),
		...game.artists.map((a) => link('boardgameartist', a)),
		...game.publishers.map((p) => link('boardgamepublisher', p)),
		...game.subdomains.map((s) => link('boardgamesubdomain', s)),
		...game.expansions.map((e) => `<link type="boardgameexpansion" id="${e.id}" value="${escapeXml(e.name)}"/>`),
		...(game.expansionOf
			? [`<link type="boardgameexpansion" id="${game.expansionOf}" value="Parent" inbound="true"/>`]
			: []),
	].join('');

	return (
		`<?xml version="1.0" encoding="utf-8"?><items><item type="boardgame" id="${game.id}">` +
		`<thumbnail>${imageBaseUrl}/images/${game.id}.svg</thumbnail>` +
		`<image>${imageBaseUrl}/images/${game.id}.svg</image>` +
		`<name type="primary" sortindex="1" value="${escapeXml(game.name)}"/>` +
		`<description>${escapeXml(game.description)}</description>` +
		`<yearpublished value="${game.year}"/>` +
		`<minplayers value="${game.minPlayers}"/><maxplayers value="${game.maxPlayers}"/>` +
		`<playingtime value="${game.playTime}"/><minplaytime value="${game.minPlayTime}"/>` +
		`<maxplaytime value="${game.maxPlayTime}"/><minage value="${game.minAge}"/>` +
		links +
		`<statistics><ratings><average value="${game.rating}"/>` +
		`<averageweight value="${game.weight}"/></ratings></statistics>` +
		`</item></items>`
	);
}

export function collectionXml(items) {
	const body = items
		.map(
			(i) =>
				`<item objecttype="thing" objectid="${i.bggId}" collid="${i.collId}">` +
				`<name sortindex="1">${escapeXml(i.name)}</name>` +
				`<yearpublished>${i.year ?? ''}</yearpublished>` +
				`<status own="1"/></item>`
		)
		.join('');
	return `<?xml version="1.0" encoding="utf-8"?><items totalitems="${items.length}">${body}</items>`;
}

export const emptyItemsXml = '<?xml version="1.0" encoding="utf-8"?><items total="0"></items>';
