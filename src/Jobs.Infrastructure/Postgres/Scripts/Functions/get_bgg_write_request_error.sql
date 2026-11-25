CREATE OR REPLACE FUNCTION get_bgg_write_request_error(p_id uuid)
RETURNS text
LANGUAGE sql
STABLE
AS $body$
	SELECT l.error
	FROM bgg_job_log l
	WHERE l.request_id = p_id
	ORDER BY l.id DESC
	LIMIT 1
$body$;
