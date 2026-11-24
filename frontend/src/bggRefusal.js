export const BGG_REMOVE_NOT_FINISHED = 'bgg-remove-not-finished';

export class BggAddRefusedError extends Error {
	constructor(gameName) {
		super(`The add of "${gameName}" was refused because its remove on BGG is not finished.`);
		this.name = 'BggAddRefusedError';
		this.gameName = gameName;
	}
}

export function refusedAddMessage(name, { expansion }) {
	const reason = "can't be added yet. Removing it from your BGG collection isn't finished. Try again later.";
	return expansion ? `The expansion "${name}" ${reason}` : `"${name}" ${reason}`;
}

export function addErrorMessage(err, { expansion }) {
	return err instanceof BggAddRefusedError ? refusedAddMessage(err.gameName, { expansion }) : err.message;
}
