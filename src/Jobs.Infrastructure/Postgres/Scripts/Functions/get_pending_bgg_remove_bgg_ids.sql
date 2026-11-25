CREATE OR REPLACE FUNCTION get_pending_bgg_remove_bgg_ids(p_account text)
RETURNS TABLE (bgg_id integer)
LANGUAGE sql
STABLE
AS $body$
	SELECT DISTINCT r.bgg_id
	FROM bgg_write_requests r
	WHERE r.kind = 'remove'
		AND r.status = 'queued'
		AND r.account = p_account
$body$;
