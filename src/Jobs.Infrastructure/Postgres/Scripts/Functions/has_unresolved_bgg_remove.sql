CREATE OR REPLACE FUNCTION has_unresolved_bgg_remove(p_account text, p_bgg_id integer)
RETURNS boolean
LANGUAGE plpgsql
STABLE
AS $body$
BEGIN
	RETURN EXISTS (
		SELECT 1
		FROM bgg_write_requests r
		LEFT JOIN hangfire.job j ON j.id = r.job_id::bigint
		WHERE r.kind = 'remove'
			AND r.account = p_account
			AND r.bgg_id = p_bgg_id
			AND (
				r.status = 'failed'
				OR (r.status = 'queued' AND (r.job_id IS NULL OR j.statename <> 'Deleted'))
			)
	);
END;
$body$;
