CREATE OR REPLACE FUNCTION cancel_bgg_write_request(
	p_id uuid,
	p_job_type text,
	p_at timestamptz
)
RETURNS boolean
LANGUAGE plpgsql
AS $body$
DECLARE
	cancelled bgg_write_requests%ROWTYPE;
BEGIN
	UPDATE bgg_write_requests
	SET status = 'cancelled',
		updated_at = p_at
	WHERE id = p_id
		AND account = 'test'
		AND status = 'queued'
	RETURNING * INTO cancelled;

	IF NOT FOUND THEN
		RETURN false;
	END IF;

	INSERT INTO bgg_job_log (request_id, job_type, account, payload, result, error, attempts, started_at, ended_at)
	VALUES (
		cancelled.id,
		p_job_type,
		cancelled.account,
		jsonb_build_object(
			'requestId', cancelled.id,
			'kind', cancelled.kind,
			'bggId', cancelled.bgg_id,
			'gameId', cancelled.game_id,
			'name', cancelled.name,
			'collectionItemId', cancelled.collection_item_id,
			'account', cancelled.account
		),
		'cancelled by the live test',
		NULL,
		cancelled.attempts,
		p_at,
		p_at
	);

	RETURN true;
END;
$body$;
