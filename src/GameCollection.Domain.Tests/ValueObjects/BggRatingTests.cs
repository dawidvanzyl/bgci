using GameCollection.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace GameCollection.Domain.Tests.ValueObjects;

public sealed class BggRatingTests
{
	[Theory]
	[InlineData(-0.1)]
	[InlineData(10.1)]
	public void Constructor_RejectsOutOfRange(double value)
	{
		Should.Throw<ArgumentException>(() => new BggRating((decimal)value));
	}

	[Fact]
	public void Constructor_RoundsToOneDecimal()
	{
		new BggRating(7.46m).Value.ShouldBe(7.5m);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(10)]
	public void Constructor_AcceptsBounds(int value)
	{
		new BggRating(value).Value.ShouldBe(value);
	}
}