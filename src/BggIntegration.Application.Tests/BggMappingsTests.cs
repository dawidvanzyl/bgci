using BggIntegration.Testing.Factories;
using Shouldly;
using Xunit;

namespace BggIntegration.Application.Tests;

public sealed class BggMappingsTests
{
	[Fact]
	public void ToAddGameCommand_CopiesCoreFields()
	{
		var command = BggGameDetailsFactory.Create(bggId: 13, name: "Catan").ToAddGameCommand();

		command.Name.ShouldBe("Catan");
		command.BggId.ShouldBe(13);
		command.BggRating.ShouldBe(7.1m);
		command.MinPlayers.ShouldBe(3);
	}

	[Fact]
	public void ToAddGameCommand_PrefersImageOverThumbnail()
	{
		var details = BggGameDetailsFactory.Create(thumbnailUrl: "thumb", imageUrl: "image");

		details.ToAddGameCommand().CoverImageUrl.ShouldBe("image");
	}

	[Fact]
	public void ToAddGameCommand_FallsBackToThumbnail()
	{
		var details = BggGameDetailsFactory.Create(thumbnailUrl: "thumb", imageUrl: null);

		details.ToAddGameCommand().CoverImageUrl.ShouldBe("thumb");
	}
}