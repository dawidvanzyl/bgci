CREATE TABLE bgg_write_requests (
	id                  uuid        PRIMARY KEY NOT NULL,
	kind                text        NOT NULL CHECK (kind IN ('add', 'remove')),
	bgg_id              integer     NOT NULL,
	game_id             uuid        NOT NULL,
	name                text        NOT NULL,
	collection_item_id  bigint      NULL,
	account             text        NOT NULL,
	idempotency_key     text        NOT NULL UNIQUE,
	status              text        NOT NULL CHECK (status IN ('queued', 'sent', 'failed')),
	job_id              text        NULL,
	attempts            integer     NOT NULL DEFAULT 0,
	created_at          timestamptz NOT NULL,
	updated_at          timestamptz NOT NULL
);
