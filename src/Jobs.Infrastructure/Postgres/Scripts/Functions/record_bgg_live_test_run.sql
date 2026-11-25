CREATE OR REPLACE FUNCTION record_bgg_live_test_run(
	p_job_type text,
	p_account text,
	p_payload text,
	p_result text,
	p_error text,
	p_started_at timestamptz,
	p_ended_at timestamptz
)
RETURNS void
LANGUAGE sql
AS $body$
	INSERT INTO bgg_job_log (request_id, job_type, account, payload, result, error, attempts, started_at, ended_at)
	VALUES (NULL, p_job_type, p_account, p_payload::jsonb, p_result, p_error, 1, p_started_at, p_ended_at);
$body$;
