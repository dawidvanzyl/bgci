namespace GameCollection.Infrastructure.Models;

internal class GameRow
{
	public string Id { get; set; } = default!;
	public string Name { get; set; } = default!;
	public int? Year { get; set; }
	public string? Description { get; set; }
	public int? MinPlayers { get; set; }
	public int? MaxPlayers { get; set; }
	public int? PlayTimeMinutes { get; set; }
	public decimal? BggRating { get; set; }
	public string? CoverImageUrl { get; set; }
	public string? Categories { get; set; }
	public string? Mechanics { get; set; }
	public int? BggId { get; set; }
	public long? BggCollId { get; set; }
	public DateTime AddedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
