CREATE OR REPLACE FUNCTION has_unfinished_earlier_bgg_write_request(p_id uuid)
RETURNS boolean
LANGUAGE plpgsql
STABLE
AS $body$
BEGIN
	RETURN EXISTS (
		SELECT 1
		FROM bgg_write_requests me
		JOIN bgg_write_requests r
			ON r.account = me.account
			AND r.bgg_id = me.bgg_id
			AND r.seq < me.seq
		LEFT JOIN hangfire.job j ON j.id = r.job_id::bigint
		WHERE me.id = p_id
			AND (
				(r.status = 'queued' AND (r.job_id IS NULL OR j.statename NOT IN ('Deleted', 'Failed')))
				OR (r.status = 'cancelled' AND j.statename = 'Processing')
			)
	);
END;
$body$;
