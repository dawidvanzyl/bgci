CREATE OR REPLACE FUNCTION complete_bgg_write_request(
	p_id uuid,
	p_status text,
	p_collection_item_id bigint,
	p_job_type text,
	p_account text,
	p_payload text,
	p_result text,
	p_error text,
	p_attempts integer,
	p_started_at timestamptz,
	p_ended_at timestamptz
)
RETURNS void
LANGUAGE sql
AS $body$
	UPDATE bgg_write_requests
	SET status = CASE WHEN status = 'cancelled' THEN status ELSE p_status END,
		collection_item_id = COALESCE(p_collection_item_id, collection_item_id),
		updated_at = p_ended_at
	WHERE id = p_id;

	INSERT INTO bgg_job_log (request_id, job_type, account, payload, result, error, attempts, started_at, ended_at)
	VALUES (p_id, p_job_type, p_account, p_payload::jsonb, p_result, p_error, p_attempts, p_started_at, p_ended_at);
$body$;
