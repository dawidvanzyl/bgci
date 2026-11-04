CREATE OR REPLACE FUNCTION create_collected_game(
	p_id uuid,
	p_name text,
	p_year integer,
	p_description text,
	p_min_players integer,
	p_max_players integer,
	p_play_time_minutes integer,
	p_min_play_time integer,
	p_max_play_time integer,
	p_bgg_rating numeric,
	p_bgg_weight numeric,
	p_min_age integer,
	p_best_player_count_min integer,
	p_best_player_count_max integer,
	p_cover_image_url text,
	p_categories text[],
	p_mechanics text[],
	p_designers text[],
	p_artists text[],
	p_publishers text[],
	p_subdomains text[],
	p_bgg_id integer,
	p_bgg_coll_id bigint,
	p_parent_game_id uuid,
	p_added_at timestamptz,
	p_updated_at timestamptz
)
RETURNS void
LANGUAGE sql
AS $body$
	INSERT INTO collected_games (
		id, name, year, description, min_players, max_players, play_time_minutes, min_play_time, max_play_time,
		bgg_rating, bgg_weight, min_age, best_player_count_min, best_player_count_max, cover_image_url,
		categories, mechanics, designers, artists, publishers, subdomains,
		bgg_id, bgg_coll_id, parent_game_id, added_at, updated_at
	)
	VALUES (
		p_id, p_name, p_year, p_description, p_min_players, p_max_players, p_play_time_minutes, p_min_play_time, p_max_play_time,
		p_bgg_rating, p_bgg_weight, p_min_age, p_best_player_count_min, p_best_player_count_max, p_cover_image_url,
		p_categories, p_mechanics, p_designers, p_artists, p_publishers, p_subdomains,
		p_bgg_id, p_bgg_coll_id, p_parent_game_id, p_added_at, p_updated_at
	);
$body$;
