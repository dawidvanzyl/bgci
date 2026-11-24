CREATE OR REPLACE FUNCTION create_bgg_write_request(
	p_id uuid,
	p_kind text,
	p_bgg_id integer,
	p_game_id uuid,
	p_name text,
	p_collection_item_id bigint,
	p_account text,
	p_idempotency_key text,
	p_created_at timestamptz
)
RETURNS boolean
LANGUAGE sql
AS $body$
	WITH inserted AS (
		INSERT INTO bgg_write_requests (
			id, kind, bgg_id, game_id, name, collection_item_id, account, idempotency_key, status, attempts, created_at, updated_at
		)
		VALUES (
			p_id, p_kind, p_bgg_id, p_game_id, p_name, p_collection_item_id, p_account, p_idempotency_key, 'queued', 0, p_created_at, p_created_at
		)
		ON CONFLICT (idempotency_key) DO NOTHING
		RETURNING 1
	)
	SELECT EXISTS (SELECT 1 FROM inserted);
$body$;
