using GameCollection.Domain.Events;
using GameCollection.Domain.ValueObjects;

namespace GameCollection.Domain.Aggregates;

public class CollectedGame
{
    private readonly List<object> _domainEvents = new();
    private readonly List<string> _categories = new();
    private readonly List<string> _mechanics = new();

    public GameId Id { get; private set; }
    public GameName Name { get; private set; }
    public int? Year { get; private set; }
    public string? Description { get; private set; }
    public PlayerCount? PlayerCount { get; private set; }
    public PlayTime? PlayTime { get; private set; }
    public BggRating? BggRating { get; private set; }
    public Uri? CoverImageUrl { get; private set; }
    public IReadOnlyList<string> Categories => _categories.AsReadOnly();
    public IReadOnlyList<string> Mechanics => _mechanics.AsReadOnly();
	public BggGameId? BggId { get; private set; }
	public long? BggCollId { get; private set; }
	public GameId? ParentGameId { get; private set; }
	public int ExpansionCount { get; private set; }
    public DateTime AddedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

	public IReadOnlyList<object> DomainEvents => _domainEvents.AsReadOnly();

	public bool IsBggSourced => BggId is not null;

    // Private constructor for reconstitution from persistence (ORM / factory use only)
#pragma warning disable CS8618
    private CollectedGame() { }
#pragma warning restore CS8618

    public static CollectedGame Create(
        GameName name,
        int? year = null,
        string? description = null,
        PlayerCount? playerCount = null,
        PlayTime? playTime = null,
        BggRating? bggRating = null,
        Uri? coverImageUrl = null,
        IEnumerable<string>? categories = null,
        IEnumerable<string>? mechanics = null,
        BggGameId? bggId = null,
        long? bggCollId = null,
        GameId? parentGameId = null)
    {
        var game = new CollectedGame
        {
            Id = GameId.New(),
            Name = name,
            Year = year,
            Description = description,
            PlayerCount = playerCount,
            PlayTime = playTime,
            BggRating = bggRating,
            CoverImageUrl = coverImageUrl,
            BggId = bggId,
            BggCollId = bggCollId,
            ParentGameId = parentGameId,
            AddedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (categories is not null)
		{
			game._categories.AddRange(categories);
		}

		if (mechanics is not null)
		{
			game._mechanics.AddRange(mechanics);
		}

		game._domainEvents.Add(new GameAddedToCollection(game.Id, game.Name));
        return game;
    }

    public void UpdateDetails(
        GameName name,
        int? year,
        string? description,
        PlayerCount? playerCount,
        PlayTime? playTime,
        BggRating? bggRating,
        Uri? coverImageUrl,
        IEnumerable<string>? categories,
        IEnumerable<string>? mechanics)
    {
        Name = name;
        Year = year;
        Description = description;
        PlayerCount = playerCount;
        PlayTime = playTime;
        BggRating = bggRating;
        CoverImageUrl = coverImageUrl;
        UpdatedAt = DateTime.UtcNow;

        _categories.Clear();
        if (categories is not null)
		{
			_categories.AddRange(categories);
		}

		_mechanics.Clear();
        if (mechanics is not null)
		{
			_mechanics.AddRange(mechanics);
		}

		_domainEvents.Add(new GameDetailsUpdated(Id, Name));
    }

	public void ClearDomainEvents() => _domainEvents.Clear();

	// Called by the repository after reconstitution to populate the computed count.
	public void SetExpansionCount(int count) => ExpansionCount = count;

	// Factory method for reconstituting from persistence (bypasses domain logic)
    public static CollectedGame Reconstitute(
        GameId id,
        GameName name,
        int? year,
        string? description,
        PlayerCount? playerCount,
        PlayTime? playTime,
        BggRating? bggRating,
        Uri? coverImageUrl,
        IEnumerable<string> categories,
        IEnumerable<string> mechanics,
        BggGameId? bggId,
        long? bggCollId,
        GameId? parentGameId,
        DateTime addedAt,
        DateTime updatedAt)
    {
        var game = new CollectedGame
        {
            Id = id,
            Name = name,
            Year = year,
            Description = description,
            PlayerCount = playerCount,
            PlayTime = playTime,
            BggRating = bggRating,
            CoverImageUrl = coverImageUrl,
            BggId = bggId,
            BggCollId = bggCollId,
            ParentGameId = parentGameId,
            AddedAt = addedAt,
            UpdatedAt = updatedAt
        };
        game._categories.AddRange(categories);
        game._mechanics.AddRange(mechanics);
        return game;
    }
}
