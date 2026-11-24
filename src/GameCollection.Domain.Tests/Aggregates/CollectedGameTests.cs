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
		var game = CollectedGame.Create(new GameName("Catan"), bggId: new BggGameId(13), bggStatus: BggStatus.Pending);

		game.IsBggSourced.ShouldBeTrue();
	}

	[Fact]
	public void ClearDomainEvents_RemovesRaisedEvents()
	{
		var game = CollectedGame.Create(new GameName("Catan"));

		game.ClearDomainEvents();

		game.DomainEvents.ShouldBeEmpty();
	}

	[Fact]
	public void Create_BggIdWithoutStatus_Throws()
	{
		Should.Throw<ArgumentException>(() => CollectedGame.Create(new GameName("Catan"), bggId: new BggGameId(13)));
	}

	[Fact]
	public void Create_StatusWithoutBggId_Throws()
	{
		Should.Throw<ArgumentException>(() => CollectedGame.Create(new GameName("Catan"), bggStatus: BggStatus.Pending));
	}

	[Fact]
	[Trait("AC", "111UC6")]
	public void MarkBggSent_PendingGame_StoresSentAndTheCollectionItemIdWithoutTouchingUpdatedAt()
	{
		var game = CollectedGame.Create(new GameName("Catan"), bggId: new BggGameId(13), bggStatus: BggStatus.Pending);
		var updatedAt = game.UpdatedAt;

		game.MarkBggSent(555);

		game.BggStatus.ShouldBe(BggStatus.Sent);
		game.BggCollId.ShouldBe(555);
		game.UpdatedAt.ShouldBe(updatedAt);
	}

	[Fact]
	[Trait("AC", "111UC6")]
	public void MarkBggSent_ManualGame_Throws()
	{
		Should.Throw<InvalidOperationException>(() => CollectedGame.Create(new GameName("Catan")).MarkBggSent(555));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[Trait("AC", "111UC6")]
	public void MarkBggSent_NonPositiveCollectionItemId_Throws(long collId)
	{
		var game = CollectedGame.Create(new GameName("Catan"), bggId: new BggGameId(13), bggStatus: BggStatus.Pending);

		Should.Throw<ArgumentOutOfRangeException>(() => game.MarkBggSent(collId));
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public void MarkBggFailed_PendingGame_StoresFailedAndKeepsEveryOtherValue()
	{
		var game = CollectedGame.Create(new GameName("Catan"), year: 1995, bggId: new BggGameId(13), bggCollId: 9, bggStatus: BggStatus.Pending);
		var updatedAt = game.UpdatedAt;

		game.MarkBggFailed();

		game.BggStatus.ShouldBe(BggStatus.Failed);
		game.BggCollId.ShouldBe(9);
		game.Year.ShouldBe(1995);
		game.Name.Value.ShouldBe("Catan");
		game.UpdatedAt.ShouldBe(updatedAt);
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public void MarkBggFailed_ManualGame_Throws()
	{
		Should.Throw<InvalidOperationException>(() => CollectedGame.Create(new GameName("Catan")).MarkBggFailed());
	}
}