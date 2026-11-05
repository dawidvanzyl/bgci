using GameCollection.Domain.Aggregates;
using GameCollection.Domain.ValueObjects;
using GameCollection.Infrastructure.Postgres;
using GameCollection.Infrastructure.Postgres.Migrations;
using GameCollection.Testing.Factories;
using GameCollection.Testing.Postgres;
using Shouldly;
using Xunit;

namespace GameCollection.Infrastructure.Tests.Postgres;

public sealed class PostgresCollectedGameRepositoryTests
{
	private static readonly TimeSpan _timestampTolerance = TimeSpan.FromMilliseconds(1);

	private readonly PostgresContainerFixture _postgres;

	public PostgresCollectedGameRepositoryTests(PostgresContainerFixture postgres)
	{
		_postgres = postgres;
	}

	[Fact]
	[Trait("AC", "64UC1")]
	public async Task GetByIdAsync_GameWithLists_ReturnsEachListInOrder()
	{
		var repository = await NewRepositoryAsync();
		var game = CollectedGameFactory.CreateDetailed(
			categories: ["Zeta", "Alpha", "Mu"],
			mechanics: ["Dice", "Auction"],
			designers: ["Yves", "Anna", "Mark"],
			artists: ["Zoe"],
			publishers: ["Tau", "Pi"],
			subdomains: ["Strategy", "Family"]);
		await repository.AddAsync(game, TestContext.Current.CancellationToken);

		var stored = await repository.GetByIdAsync(game.Id, TestContext.Current.CancellationToken);

		stored.ShouldNotBeNull();
		stored.Categories.ShouldBe(["Zeta", "Alpha", "Mu"]);
		stored.Mechanics.ShouldBe(["Dice", "Auction"]);
		stored.Designers.ShouldBe(["Yves", "Anna", "Mark"]);
		stored.Artists.ShouldBe(["Zoe"]);
		stored.Publishers.ShouldBe(["Tau", "Pi"]);
		stored.Subdomains.ShouldBe(["Strategy", "Family"]);
	}

	[Fact]
	[Trait("AC", "64UC1")]
	public async Task GetByIdAsync_GameWithEmptyLists_ReturnsEmptyLists()
	{
		var repository = await NewRepositoryAsync();
		var game = CollectedGameFactory.CreateDetailed(categories: ["Alpha"]);
		await repository.AddAsync(game, TestContext.Current.CancellationToken);

		var stored = await repository.GetByIdAsync(game.Id, TestContext.Current.CancellationToken);

		stored.ShouldNotBeNull();
		stored.Mechanics.ShouldBeEmpty();
		stored.Designers.ShouldBeEmpty();
		stored.Artists.ShouldBeEmpty();
		stored.Publishers.ShouldBeEmpty();
		stored.Subdomains.ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "64UC1")]
	public async Task GetByIdAsync_GameWithEveryOptionalValueNull_ReturnsGameWithNulls()
	{
		var repository = await NewRepositoryAsync();
		var game = CollectedGameFactory.CreateManual("Bare");
		await repository.AddAsync(game, TestContext.Current.CancellationToken);

		var stored = await repository.GetByIdAsync(game.Id, TestContext.Current.CancellationToken);

		stored.ShouldNotBeNull();
		stored.Name.Value.ShouldBe("Bare");
		stored.Year.ShouldBeNull();
		stored.PlayerCount.ShouldBeNull();
		stored.BggRating.ShouldBeNull();
		stored.BggId.ShouldBeNull();
		stored.BggCollId.ShouldBeNull();
		stored.ParentGameId.ShouldBeNull();
		stored.CoverImageUrl.ShouldBeNull();
		stored.Categories.ShouldBeEmpty();
		stored.AddedAt.ShouldBe(game.AddedAt, _timestampTolerance);
		stored.UpdatedAt.ShouldBe(game.UpdatedAt, _timestampTolerance);
	}

	[Fact]
	[Trait("AC", "64UC2")]
	public async Task GetByIdAsync_ValuesWithSeparatorCharacters_ReturnsEachValueUnchanged()
	{
		var repository = await NewRepositoryAsync();
		string[] values = ["Card|Driven", "Hand, Management", "The \"Big\" One", "a|b,c\"d"];
		var game = CollectedGameFactory.CreateDetailed(categories: values, mechanics: values, designers: values, artists: values, publishers: values, subdomains: values);
		await repository.AddAsync(game, TestContext.Current.CancellationToken);

		var stored = await repository.GetByIdAsync(game.Id, TestContext.Current.CancellationToken);

		stored.ShouldNotBeNull();
		stored.Categories.ShouldBe(values);
		stored.Mechanics.ShouldBe(values);
		stored.Designers.ShouldBe(values);
		stored.Artists.ShouldBe(values);
		stored.Publishers.ShouldBe(values);
		stored.Subdomains.ShouldBe(values);
	}

	[Fact]
	[Trait("AC", "64UC3")]
	public async Task UpdateAsync_ChangedDetailsAndLists_ReturnsTheChange()
	{
		var repository = await NewRepositoryAsync();
		var game = CollectedGameFactory.CreateDetailed(
			name: "Before",
			categories: ["One", "Two", "Three"],
			mechanics: ["Dice"],
			designers: ["Anna", "Bob"]);
		await repository.AddAsync(game, TestContext.Current.CancellationToken);

		game.UpdateDetails(
			new GameName("After"),
			2021,
			"Changed description",
			new PlayerCount(2, 6),
			new PlayTime(45),
			30,
			60,
			new BggRating(6.5m),
			new BggWeight(3.25m),
			10,
			3,
			5,
			null,
			["Three", "One"],
			[],
			["Bob"],
			["New artist"],
			null,
			null);
		await repository.UpdateAsync(game, TestContext.Current.CancellationToken);
		var stored = await repository.GetByIdAsync(game.Id, TestContext.Current.CancellationToken);

		stored.ShouldNotBeNull();
		stored.Name.Value.ShouldBe("After");
		stored.Year.ShouldBe(2021);
		stored.Description.ShouldBe("Changed description");
		stored.PlayerCount.ShouldBe(new PlayerCount(2, 6));
		stored.PlayTime.ShouldBe(new PlayTime(45));
		stored.CoverImageUrl.ShouldBeNull();
		stored.Categories.ShouldBe(["Three", "One"]);
		stored.Mechanics.ShouldBeEmpty();
		stored.Designers.ShouldBe(["Bob"]);
		stored.Artists.ShouldBe(["New artist"]);
		stored.UpdatedAt.ShouldBe(game.UpdatedAt, _timestampTolerance);
		stored.AddedAt.ShouldBe(game.AddedAt, _timestampTolerance);
	}

	[Fact]
	[Trait("AC", "64UC4")]
	public async Task DeleteAsync_ExistingGame_RemovesOnlyThatGame()
	{
		var repository = await NewRepositoryAsync();
		var removed = CollectedGameFactory.CreateManual("Removed");
		var kept = CollectedGameFactory.CreateManual("Kept");
		await repository.AddAsync(removed, TestContext.Current.CancellationToken);
		await repository.AddAsync(kept, TestContext.Current.CancellationToken);

		await repository.DeleteAsync(removed.Id, TestContext.Current.CancellationToken);

		(await repository.GetByIdAsync(removed.Id, TestContext.Current.CancellationToken)).ShouldBeNull();
		var all = await repository.GetAllAsync(TestContext.Current.CancellationToken);
		all.Select(game => game.Id).ShouldBe([kept.Id]);
	}

	[Fact]
	[Trait("AC", "64UC5")]
	public async Task GetByIdAsync_BggDetails_ReturnsEveryValueUnchanged()
	{
		var repository = await NewRepositoryAsync();
		var collectionId = long.MaxValue - 5;
		var game = CollectedGameFactory.CreateDetailed(
			bggId: 174430,
			bggCollId: collectionId,
			bggRating: 8.2m,
			bggWeight: 3.9m,
			minAge: 14,
			minPlayers: 1,
			maxPlayers: 4,
			playTimeMinutes: 120,
			minPlayTimeMinutes: 90,
			maxPlayTimeMinutes: 150,
			bestPlayerCountMin: 2,
			bestPlayerCountMax: 3);
		await repository.AddAsync(game, TestContext.Current.CancellationToken);

		var stored = await repository.GetByIdAsync(game.Id, TestContext.Current.CancellationToken);

		stored.ShouldNotBeNull();
		stored.BggId.ShouldBe(game.BggId);
		stored.BggCollId.ShouldBe(collectionId);
		stored.BggRating.ShouldBe(game.BggRating);
		stored.BggWeight.ShouldBe(game.BggWeight);
		stored.MinAge.ShouldBe(14);
		stored.PlayerCount.ShouldBe(new PlayerCount(1, 4));
		stored.PlayTime.ShouldBe(new PlayTime(120));
		stored.MinPlayTimeMinutes.ShouldBe(90);
		stored.MaxPlayTimeMinutes.ShouldBe(150);
		stored.BestPlayerCountMin.ShouldBe(2);
		stored.BestPlayerCountMax.ShouldBe(3);
	}

	[Fact]
	[Trait("AC", "64UC6")]
	public async Task GetByIdAsync_ParentWithTwoExpansions_ReturnsExpansionCountTwo()
	{
		var repository = await NewRepositoryAsync();
		var parent = CollectedGameFactory.CreateManual("Parent");
		await repository.AddAsync(parent, TestContext.Current.CancellationToken);
		await repository.AddAsync(CollectedGameFactory.CreateDetailed(name: "First", parentGameId: parent.Id), TestContext.Current.CancellationToken);
		await repository.AddAsync(CollectedGameFactory.CreateDetailed(name: "Second", parentGameId: parent.Id), TestContext.Current.CancellationToken);

		var stored = await repository.GetByIdAsync(parent.Id, TestContext.Current.CancellationToken);

		stored.ShouldNotBeNull();
		stored.ExpansionCount.ShouldBe(2);
	}

	[Fact]
	[Trait("AC", "64UC6")]
	public async Task GetAllAsync_ParentWithTwoExpansions_ReturnsCountsAndParentLinks()
	{
		var repository = await NewRepositoryAsync();
		var parent = CollectedGameFactory.CreateManual("Parent");
		var first = CollectedGameFactory.CreateDetailed(name: "First", parentGameId: parent.Id);
		var second = CollectedGameFactory.CreateDetailed(name: "Second", parentGameId: parent.Id);
		await repository.AddAsync(parent, TestContext.Current.CancellationToken);
		await repository.AddAsync(first, TestContext.Current.CancellationToken);
		await repository.AddAsync(second, TestContext.Current.CancellationToken);

		var all = await repository.GetAllAsync(TestContext.Current.CancellationToken);

		all.Single(game => game.Id == parent.Id).ExpansionCount.ShouldBe(2);
		foreach (var expansion in all.Where(game => game.Id != parent.Id))
		{
			expansion.ParentGameId.ShouldBe(parent.Id);
			expansion.ExpansionCount.ShouldBe(0);
		}
	}

	[Fact]
	[Trait("AC", "64UC7")]
	public async Task GetAllAsync_BggAndManualGames_ReturnsEachWithItsOwnBggId()
	{
		var repository = await NewRepositoryAsync();
		var first = CollectedGameFactory.CreateFromBgg("First", bggId: 101);
		var second = CollectedGameFactory.CreateFromBgg("Second", bggId: 202);
		var manual = CollectedGameFactory.CreateManual("Manual");
		await repository.AddAsync(first, TestContext.Current.CancellationToken);
		await repository.AddAsync(second, TestContext.Current.CancellationToken);
		await repository.AddAsync(manual, TestContext.Current.CancellationToken);

		var all = await repository.GetAllAsync(TestContext.Current.CancellationToken);

		all.Single(game => game.Id == first.Id).BggId.ShouldBe(new BggGameId(101));
		all.Single(game => game.Id == second.Id).BggId.ShouldBe(new BggGameId(202));
		var storedManual = all.Single(game => game.Id == manual.Id);
		storedManual.BggId.ShouldBeNull();
		storedManual.IsBggSourced.ShouldBeFalse();
		all.Single(game => game.Id == first.Id).IsBggSourced.ShouldBeTrue();
	}

	[Fact]
	[Trait("AC", "64UC8")]
	public async Task GetAllAsync_FreshlyMigratedDatabase_ReturnsEmptyList()
	{
		var repository = await NewRepositoryAsync();

		var all = await repository.GetAllAsync(TestContext.Current.CancellationToken);

		all.ShouldBeEmpty();
	}

	private async Task<PostgresCollectedGameRepository> NewRepositoryAsync()
	{
		var connectionString = await _postgres.CreateDatabaseAsync();
		await new DatabaseMigrationRunner(connectionString, MigrationScripts.LoadEmbedded()).RunAsync(TestContext.Current.CancellationToken);
		return new PostgresCollectedGameRepository(connectionString);
	}
}