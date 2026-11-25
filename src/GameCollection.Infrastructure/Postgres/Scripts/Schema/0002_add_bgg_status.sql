ALTER TABLE collected_games
	ADD COLUMN bgg_status text NULL CHECK (bgg_status IN ('pending', 'sent', 'failed'));

UPDATE collected_games SET bgg_status = 'sent' WHERE bgg_id IS NOT NULL;

ALTER TABLE collected_games
	ADD CONSTRAINT collected_games_bgg_status_matches_source CHECK ((bgg_id IS NULL) = (bgg_status IS NULL));

DROP FUNCTION IF EXISTS create_collected_game(
	uuid, text, integer, text, integer, integer, integer, integer, integer, numeric, numeric, integer,
	integer, integer, text, text[], text[], text[], text[], text[], text[], integer, bigint, uuid,
	timestamptz, timestamptz
);

DROP FUNCTION IF EXISTS get_collected_games();

DROP FUNCTION IF EXISTS get_collected_game_by_id(uuid);
