using GameCollection.Domain.Aggregates;
using GameCollection.Domain.Events;
using GameCollection.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace GameCollection.Domain.Tests.Aggregates;

public sealed class CollectedGameTests
{
	[Fact]
	public void Create_RaisesGameAddedToCollection()
	{
		var game = CollectedGame.Create(new GameName("Catan"));

		game.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<GameAddedToCollection>();
	}

	[Fact]
	public void Create_WithoutBggId_IsNotBggSourced()
	{
		CollectedGame.Create(new GameName("Catan")).IsBggSourced.ShouldBeFalse();
	}

	[Fact]
	public void Create_WithBggId_IsBggSourced()
	{
		var game = CollectedGame.Create(new GameName("Catan"), bggId: new BggGameId(13));

		game.IsBggSourced.ShouldBeTrue();
	}

	[Fact]
	public void ClearDomainEvents_RemovesRaisedEvents()
	{
		var game = CollectedGame.Create(new GameName("Catan"));

		game.ClearDomainEvents();

		game.DomainEvents.ShouldBeEmpty();
	}
}