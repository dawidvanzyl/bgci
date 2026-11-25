CREATE OR REPLACE FUNCTION start_bgg_write_attempt(p_id uuid, p_at timestamptz)
RETURNS integer
LANGUAGE sql
AS $body$
	UPDATE bgg_write_requests
	SET attempts = attempts + 1,
		updated_at = p_at
	WHERE id = p_id
		AND status IN ('queued', 'failed')
	RETURNING attempts;
$body$;
