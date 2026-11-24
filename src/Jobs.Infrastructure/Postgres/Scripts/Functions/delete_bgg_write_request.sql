CREATE OR REPLACE FUNCTION delete_bgg_write_request(p_id uuid)
RETURNS void
LANGUAGE sql
AS $body$
	DELETE FROM bgg_write_requests WHERE id = p_id;
$body$;
