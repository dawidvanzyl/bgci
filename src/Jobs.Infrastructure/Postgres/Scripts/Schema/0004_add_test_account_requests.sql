ALTER TABLE bgg_write_requests ALTER COLUMN game_id DROP NOT NULL;

ALTER TABLE bgg_write_requests
	ADD CONSTRAINT bgg_write_requests_account_check CHECK (account IN ('owner', 'test'));

ALTER TABLE bgg_write_requests
	ADD CONSTRAINT bgg_write_requests_game_by_account_check CHECK ((account = 'owner') = (game_id IS NOT NULL));

ALTER TABLE bgg_job_log ALTER COLUMN request_id DROP NOT NULL;
