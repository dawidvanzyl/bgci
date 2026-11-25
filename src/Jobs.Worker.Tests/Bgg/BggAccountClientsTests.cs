using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using Jobs.Worker.Bgg;
using Jobs.Worker.LiveTest;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace Jobs.Worker.Tests.Bgg;

public sealed class BggAccountClientsTests : IDisposable
{
	private readonly string _directory = Directory.CreateTempSubdirectory("accounts").FullName;
	private readonly Mock<IHttpClientFactory> _httpClientFactory = new();

	public BggAccountClientsTests()
	{
		_httpClientFactory
			.Setup(f => f.CreateClient(It.IsAny<string>()))
			.Returns(() => new HttpClient());
	}

	[Fact]
	public void Get_TestAccountOnTheAllowlist_ReturnsAClient()
	{
		var clients = CreateClients(testUsername: "bgci_tester");

		var client = clients.Get("test");

		client.Settings.Username.ShouldBe("bgci_tester");
		_httpClientFactory.Verify(f => f.CreateClient(BggAccountClients.BggWriteClientName), Times.Once);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Get_TestUsernameBlank_ThrowsWithoutCreatingAClient(string testUsername)
	{
		var clients = CreateClients(testUsername: testUsername);

		AssertRefused(() => clients.Get("test"));
	}

	[Fact]
	public void Get_TestUsernameNotOnTheAllowlist_ThrowsWithoutCreatingAClient()
	{
		var clients = CreateClients(testUsername: "someone_else");

		AssertRefused(() => clients.Get("test"));
	}

	[Fact]
	public void Get_TestUsernameEqualsOwnerUsername_ThrowsWithoutCreatingAClient()
	{
		var clients = CreateClients(ownerUsername: "BGCI_Tester", testUsername: "bgci_tester");

		AssertRefused(() => clients.Get("test"));
	}

	[Fact]
	public void Get_OwnerAccount_ReturnsAClientWithoutTheAllowlist()
	{
		var clients = CreateClients(ownerUsername: "owner_user", testUsername: "someone_else");

		var client = clients.Get("owner");

		client.Settings.Username.ShouldBe("owner_user");
	}

	[Fact]
	public void Get_UnknownAccount_Throws()
	{
		var clients = CreateClients();

		var exception = Should.Throw<BggWriteException>(() => clients.Get("other"));

		exception.Step.ShouldBe("configuration");
		_httpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
	}

	public void Dispose()
	{
		Directory.Delete(_directory, recursive: true);
	}

	private void AssertRefused(Action get)
	{
		var exception = Should.Throw<BggWriteException>(get);

		exception.Step.ShouldBe("configuration");
		_httpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
	}

	private BggAccountClients CreateClients(string ownerUsername = "owner_user", string testUsername = "bgci_tester")
	{
		File.WriteAllText(
			Path.Combine(_directory, BggTestAccountAllowlist.FileName),
			"""{ "usernames": ["bgci_tester"] }""");
		return new BggAccountClients(
			_httpClientFactory.Object,
			TimeProvider.System,
			NullLogger<BggWriteHttpClient>.Instance,
			BggTestAccountAllowlist.Load(_directory),
			new BggSettings { Username = ownerUsername, Password = "owner-password", BearerToken = "owner-token" },
			new BggSettings { Username = testUsername, Password = "test-password", BearerToken = "test-token" });
	}
}