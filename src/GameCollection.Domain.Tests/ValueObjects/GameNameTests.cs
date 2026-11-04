using GameCollection.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace GameCollection.Domain.Tests.ValueObjects;

public sealed class GameNameTests
{
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Constructor_RejectsEmptyOrWhitespace(string value)
	{
		Should.Throw<ArgumentException>(() => new GameName(value));
	}

	[Fact]
	public void Constructor_TrimsValue()
	{
		new GameName("  Catan  ").Value.ShouldBe("Catan");
	}
}