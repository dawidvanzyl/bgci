CREATE OR REPLACE FUNCTION get_bgg_write_request(p_id uuid)
RETURNS TABLE (
	id uuid,
	kind text,
	bgg_id integer,
	game_id uuid,
	name text,
	collection_item_id bigint,
	account text,
	status text,
	attempts integer
)
LANGUAGE sql
STABLE
AS $body$
	SELECT r.id, r.kind, r.bgg_id, r.game_id, r.name, r.collection_item_id, r.account, r.status, r.attempts
	FROM bgg_write_requests r
	WHERE r.id = p_id
$body$;
