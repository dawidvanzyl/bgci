ALTER TABLE bgg_write_requests DROP CONSTRAINT bgg_write_requests_status_check;

ALTER TABLE bgg_write_requests
	ADD CONSTRAINT bgg_write_requests_status_check CHECK (status IN ('queued', 'sent', 'failed', 'cancelled'));

ALTER TABLE bgg_write_requests ADD COLUMN seq bigint GENERATED ALWAYS AS IDENTITY;

CREATE INDEX bgg_write_requests_account_bgg_id_idx ON bgg_write_requests (account, bgg_id, seq);

DROP FUNCTION IF EXISTS record_bgg_write_attempt(uuid, timestamptz);
