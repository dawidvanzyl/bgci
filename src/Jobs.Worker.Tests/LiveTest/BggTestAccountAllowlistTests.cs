using Jobs.Worker.LiveTest;
using Shouldly;
using Xunit;

namespace Jobs.Worker.Tests.LiveTest;

public sealed class BggTestAccountAllowlistTests : IDisposable
{
	private readonly string _directory = Directory.CreateTempSubdirectory("allowlist").FullName;

	[Fact]
	public void Contains_UsernameOnTheList_ReturnsTrue()
	{
		WriteFile("""{ "usernames": ["bgci_tester"] }""");

		BggTestAccountAllowlist.Load(_directory).Contains("bgci_tester").ShouldBeTrue();
	}

	[Fact]
	public void Contains_UsernameInOtherCaseWithSpaces_ReturnsTrue()
	{
		WriteFile("""{ "usernames": ["  BGCI_Tester "] }""");

		BggTestAccountAllowlist.Load(_directory).Contains(" bgci_tester  ").ShouldBeTrue();
	}

	[Fact]
	public void Contains_UsernameNotOnTheList_ReturnsFalse()
	{
		WriteFile("""{ "usernames": ["bgci_tester"] }""");

		BggTestAccountAllowlist.Load(_directory).Contains("someone_else").ShouldBeFalse();
	}

	[Fact]
	public void Load_MissingFile_AllowsNobody()
	{
		BggTestAccountAllowlist.Load(_directory).Contains("bgci_tester").ShouldBeFalse();
	}

	[Fact]
	public void Load_NoUsernames_AllowsNobody()
	{
		WriteFile("""{ "usernames": [] }""");

		var allowlist = BggTestAccountAllowlist.Load(_directory);

		allowlist.Contains("bgci_tester").ShouldBeFalse();
		allowlist.Contains(string.Empty).ShouldBeFalse();
	}

	public void Dispose()
	{
		Directory.Delete(_directory, recursive: true);
	}

	private void WriteFile(string json)
	{
		File.WriteAllText(Path.Combine(_directory, BggTestAccountAllowlist.FileName), json);
	}
}