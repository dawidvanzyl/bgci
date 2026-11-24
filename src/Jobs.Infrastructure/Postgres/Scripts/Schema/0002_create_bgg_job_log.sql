CREATE TABLE bgg_job_log (
	id          bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	request_id  uuid        NOT NULL,
	job_type    text        NOT NULL,
	account     text        NOT NULL,
	payload     jsonb       NOT NULL,
	result      text        NULL,
	error       text        NULL,
	attempts    integer     NOT NULL,
	started_at  timestamptz NOT NULL,
	ended_at    timestamptz NOT NULL
);
