ALTER TABLE bgg_write_requests DROP CONSTRAINT bgg_write_requests_status_check;

ALTER TABLE bgg_write_requests
	ADD CONSTRAINT bgg_write_requests_status_check CHECK (status IN ('queued', 'sent', 'failed', 'cancelled'));

ALTER TABLE bgg_write_requests ADD COLUMN seq bigint;

UPDATE bgg_write_requests r
SET seq = ordered.position
FROM (
	SELECT id, row_number() OVER (ORDER BY created_at, id) AS position
	FROM bgg_write_requests
) ordered
WHERE r.id = ordered.id;

ALTER TABLE bgg_write_requests ALTER COLUMN seq SET NOT NULL;

ALTER TABLE bgg_write_requests ALTER COLUMN seq ADD GENERATED ALWAYS AS IDENTITY;

SELECT setval(
	pg_get_serial_sequence('bgg_write_requests', 'seq'),
	COALESCE((SELECT MAX(seq) FROM bgg_write_requests), 0) + 1,
	false);

CREATE INDEX bgg_write_requests_account_bgg_id_idx ON bgg_write_requests (account, bgg_id, seq);

DROP FUNCTION IF EXISTS record_bgg_write_attempt(uuid, timestamptz);
