CREATE OR REPLACE FUNCTION set_bgg_write_request_job(p_id uuid, p_job_id text)
RETURNS void
LANGUAGE sql
AS $body$
	UPDATE bgg_write_requests SET job_id = p_job_id WHERE id = p_id;
$body$;
