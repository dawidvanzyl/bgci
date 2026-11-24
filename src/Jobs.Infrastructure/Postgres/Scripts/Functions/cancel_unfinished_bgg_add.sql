CREATE OR REPLACE FUNCTION cancel_unfinished_bgg_add(
	p_game_id uuid,
	p_account text,
	p_job_type text,
	p_at timestamptz
)
RETURNS integer
LANGUAGE plpgsql
AS $body$
DECLARE
	cancelled bgg_write_requests%ROWTYPE;
BEGIN
	UPDATE bgg_write_requests
	SET status = 'cancelled',
		updated_at = p_at
	WHERE kind = 'add'
		AND game_id = p_game_id
		AND account = p_account
		AND status = 'queued'
	RETURNING * INTO cancelled;

	IF NOT FOUND THEN
		RETURN NULL;
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
		'cancelled by a remove',
		NULL,
		cancelled.attempts,
		p_at,
		p_at
	);

	RETURN cancelled.attempts;
END;
$body$;
