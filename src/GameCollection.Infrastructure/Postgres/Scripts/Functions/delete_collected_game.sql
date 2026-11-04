CREATE OR REPLACE FUNCTION delete_collected_game(p_id uuid)
RETURNS void
LANGUAGE sql
AS $body$
	DELETE FROM collected_games WHERE id = p_id;
$body$;
