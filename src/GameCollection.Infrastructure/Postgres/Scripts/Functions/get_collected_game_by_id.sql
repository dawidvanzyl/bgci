CREATE OR REPLACE FUNCTION get_collected_game_by_id(p_id uuid)
RETURNS TABLE (
	id uuid,
	name text,
	year integer,
	description text,
	min_players integer,
	max_players integer,
	play_time_minutes integer,
	min_play_time integer,
	max_play_time integer,
	bgg_rating numeric,
	bgg_weight numeric,
	min_age integer,
	best_player_count_min integer,
	best_player_count_max integer,
	cover_image_url text,
	categories text[],
	mechanics text[],
	designers text[],
	artists text[],
	publishers text[],
	subdomains text[],
	bgg_id integer,
	bgg_coll_id bigint,
	parent_game_id uuid,
	added_at timestamptz,
	updated_at timestamptz,
	expansion_count integer
)
LANGUAGE sql
STABLE
AS $body$
	SELECT
		g.id,
		g.name,
		g.year,
		g.description,
		g.min_players,
		g.max_players,
		g.play_time_minutes,
		g.min_play_time,
		g.max_play_time,
		g.bgg_rating,
		g.bgg_weight,
		g.min_age,
		g.best_player_count_min,
		g.best_player_count_max,
		g.cover_image_url,
		g.categories,
		g.mechanics,
		g.designers,
		g.artists,
		g.publishers,
		g.subdomains,
		g.bgg_id,
		g.bgg_coll_id,
		g.parent_game_id,
		g.added_at,
		g.updated_at,
		(SELECT count(*)::integer FROM collected_games e WHERE e.parent_game_id = g.id)
	FROM collected_games g
	WHERE g.id = p_id
$body$;
