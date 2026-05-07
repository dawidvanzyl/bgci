using GameCollection.Domain.Events;
using GameCollection.Domain.ValueObjects;

namespace GameCollection.Domain.Aggregates;

public class CollectedGame
{
    private readonly List<object> _domainEvents = new();
    private readonly List<string> _categories = new();
    private readonly List<string> _mechanics = new();
    private readonly List<string> _designers = new();
    private readonly List<string> _artists = new();
    private readonly List<string> _publishers = new();
    private readonly List<string> _subdomains = new();

    public GameId Id { get; private set; }
    public GameName Name { get; private set; }
    public int? Year { get; private set; }
    public string? Description { get; private set; }
    public PlayerCount? PlayerCount { get; private set; }
    public PlayTime? PlayTime { get; private set; }
    public int? MinPlayTimeMinutes { get; private set; }
    public int? MaxPlayTimeMinutes { get; private set; }
    public BggRating? BggRating { get; private set; }
    public BggWeight? BggWeight { get; private set; }
    public int? MinAge { get; private set; }
    public Uri? CoverImageUrl { get; private set; }
    public IReadOnlyList<string> Categories => _categories.AsReadOnly();
    public IReadOnlyList<string> Mechanics => _mechanics.AsReadOnly();
    public IReadOnlyList<string> Designers => _designers.AsReadOnly();
    public IReadOnlyList<string> Artists => _artists.AsReadOnly();
    public IReadOnlyList<string> Publishers => _publishers.AsReadOnly();
    public IReadOnlyList<string> Subdomains => _subdomains.AsReadOnly();
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
        int? minPlayTimeMinutes = null,
        int? maxPlayTimeMinutes = null,
        BggRating? bggRating = null,
        BggWeight? bggWeight = null,
        int? minAge = null,
        Uri? coverImageUrl = null,
        IEnumerable<string>? categories = null,
        IEnumerable<string>? mechanics = null,
        IEnumerable<string>? designers = null,
        IEnumerable<string>? artists = null,
        IEnumerable<string>? publishers = null,
        IEnumerable<string>? subdomains = null,
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
            MinPlayTimeMinutes = minPlayTimeMinutes,
            MaxPlayTimeMinutes = maxPlayTimeMinutes,
            BggRating = bggRating,
            BggWeight = bggWeight,
            MinAge = minAge,
            CoverImageUrl = coverImageUrl,
            BggId = bggId,
            BggCollId = bggCollId,
            ParentGameId = parentGameId,
            AddedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (categories is not null) game._categories.AddRange(categories);
        if (mechanics is not null) game._mechanics.AddRange(mechanics);
        if (designers is not null) game._designers.AddRange(designers);
        if (artists is not null) game._artists.AddRange(artists);
        if (publishers is not null) game._publishers.AddRange(publishers);
        if (subdomains is not null) game._subdomains.AddRange(subdomains);

		game._domainEvents.Add(new GameAddedToCollection(game.Id, game.Name));
        return game;
    }

    public void UpdateDetails(
        GameName name,
        int? year,
        string? description,
        PlayerCount? playerCount,
        PlayTime? playTime,
        int? minPlayTimeMinutes,
        int? maxPlayTimeMinutes,
        BggRating? bggRating,
        BggWeight? bggWeight,
        int? minAge,
        Uri? coverImageUrl,
        IEnumerable<string>? categories,
        IEnumerable<string>? mechanics,
        IEnumerable<string>? designers,
        IEnumerable<string>? artists,
        IEnumerable<string>? publishers,
        IEnumerable<string>? subdomains)
    {
        Name = name;
        Year = year;
        Description = description;
        PlayerCount = playerCount;
        PlayTime = playTime;
        MinPlayTimeMinutes = minPlayTimeMinutes;
        MaxPlayTimeMinutes = maxPlayTimeMinutes;
        BggRating = bggRating;
        BggWeight = bggWeight;
        MinAge = minAge;
        CoverImageUrl = coverImageUrl;
        UpdatedAt = DateTime.UtcNow;

        _categories.Clear();
        if (categories is not null) _categories.AddRange(categories);

        _mechanics.Clear();
        if (mechanics is not null) _mechanics.AddRange(mechanics);

        _designers.Clear();
        if (designers is not null) _designers.AddRange(designers);

        _artists.Clear();
        if (artists is not null) _artists.AddRange(artists);

        _publishers.Clear();
        if (publishers is not null) _publishers.AddRange(publishers);

        _subdomains.Clear();
        if (subdomains is not null) _subdomains.AddRange(subdomains);

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
        int? minPlayTimeMinutes,
        int? maxPlayTimeMinutes,
        BggRating? bggRating,
        BggWeight? bggWeight,
        int? minAge,
        Uri? coverImageUrl,
        IEnumerable<string> categories,
        IEnumerable<string> mechanics,
        IEnumerable<string> designers,
        IEnumerable<string> artists,
        IEnumerable<string> publishers,
        IEnumerable<string> subdomains,
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
            MinPlayTimeMinutes = minPlayTimeMinutes,
            MaxPlayTimeMinutes = maxPlayTimeMinutes,
            BggRating = bggRating,
            BggWeight = bggWeight,
            MinAge = minAge,
            CoverImageUrl = coverImageUrl,
            BggId = bggId,
            BggCollId = bggCollId,
            ParentGameId = parentGameId,
            AddedAt = addedAt,
            UpdatedAt = updatedAt
        };
        game._categories.AddRange(categories);
        game._mechanics.AddRange(mechanics);
        game._designers.AddRange(designers);
        game._artists.AddRange(artists);
        game._publishers.AddRange(publishers);
        game._subdomains.AddRange(subdomains);
        return game;
    }
}
