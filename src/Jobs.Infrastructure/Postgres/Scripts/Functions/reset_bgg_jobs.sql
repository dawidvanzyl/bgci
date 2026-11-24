CREATE OR REPLACE FUNCTION reset_bgg_jobs()
RETURNS void
LANGUAGE sql
AS $body$
	TRUNCATE bgg_write_requests, bgg_job_log;
$body$;
