CREATE OR REPLACE FUNCTION update_collected_game_bgg_status(
	p_id uuid,
	p_bgg_status text,
	p_bgg_coll_id bigint
)
RETURNS void
LANGUAGE sql
AS $body$
	UPDATE collected_games
	SET bgg_status = p_bgg_status,
		bgg_coll_id = p_bgg_coll_id
	WHERE id = p_id;
$body$;
