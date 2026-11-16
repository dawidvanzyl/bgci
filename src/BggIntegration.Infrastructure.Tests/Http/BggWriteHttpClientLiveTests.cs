using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using Microsoft.Extensions.Options;
using System.Globalization;
using Xunit;

namespace BggIntegration.Infrastructure.Tests.Http;

public sealed class BggWriteHttpClientLiveTests
{
	private const string OptInVariable = "BGG_LIVE_TEST";
	private const int LiveBggId = 3179;
	private const int PollListings = 6;
	private const int ConfirmingListings = 3;

	private static readonly Uri _bggAddress = new("https://boardgamegeek.com/");
	private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan _runLimit = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan _cleanupLimit = TimeSpan.FromMinutes(2);

	[Fact]
	[Trait("Category", "LiveBgg")]
	public async Task AddAndRemove_TestAccount_LeavesCollectionUnchanged()
	{
		Assert.SkipUnless(IsOptedIn(), $"Set {OptInVariable}=true to run the live BGG write test.");

		var settings = LiveBggSettings.FromEnvironment();
		var redactor = new SecretRedactor(settings.Password, settings.BearerToken);
		var entry = CheckGuards(settings, redactor);

		var output = TestContext.Current.TestOutputHelper;
		void Log(string line) => output?.WriteLine(redactor.Redact(line));

		using var writeHandler = CreateHandler();
		using var readHandler = CreateHandler();
		using var writeHttp = CreateHttpClient(writeHandler);
		using var readHttp = CreateHttpClient(readHandler);
		var reader = new LiveBggCollectionReader(readHttp, settings.Username, settings.BearerToken);
		var client = new BggWriteHttpClient(
			writeHttp,
			new BggSession(),
			Options.Create(new BggSettings { Username = settings.Username, Password = settings.Password, BearerToken = settings.BearerToken }),
			TimeProvider.System,
			new TestOutputLogger<BggWriteHttpClient>(output, redactor));

		var failures = new List<LiveFailure>();
		var phase = LiveFailureClassifier.CleanupStep;
		var addSent = false;
		var confirmedGone = false;

		using var runSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		runSource.CancelAfter(_runLimit);
		var runToken = runSource.Token;

		try
		{
			Log("step: cleanup (before)");
			var leftover = await reader.ListCollectionItemIdsAsync(LiveBggId, runToken);
			if (leftover.Count > 0)
			{
				await RemoveAllAsync(client, leftover, runToken);
				if (!await WaitUntilGoneAsync(reader, runToken))
				{
					failures.Add(new LiveFailure(phase, LiveFailureKind.StillListed, $"{entry.Name} is still listed after the cleanup before the add."));
				}
			}

			if (failures.Count == 0)
			{
				phase = LiveFailureClassifier.AddStep;
				Log("step: add");
				addSent = true;
				var collId = await client.AddToCollectionAsync(LiveBggId, entry.Name, runToken);

				phase = LiveFailureClassifier.RemoveStep;
				Log($"step: remove (collection item id {collId.ToString(CultureInfo.InvariantCulture)})");
				await client.RemoveFromCollectionAsync(collId, runToken);

				if (await WaitUntilGoneAsync(reader, runToken))
				{
					confirmedGone = true;
				}
				else
				{
					failures.Add(new LiveFailure(phase, LiveFailureKind.StillListed, $"{entry.Name} is still listed after the remove."));
				}
			}
		}
		catch (Exception ex)
		{
			failures.Add(LiveFailureClassifier.Classify(ex, phase, runToken));
		}

		using var cleanupSource = new CancellationTokenSource(_cleanupLimit);
		var cleanupToken = cleanupSource.Token;
		try
		{
			Log("step: cleanup (after)");
			var cleanupFailure = addSent && !confirmedGone
				? await SweepForLateListingAsync(reader, client, cleanupToken)
				: await RemoveListedOnceAsync(reader, client, cleanupToken);
			if (cleanupFailure is not null)
			{
				failures.Add(cleanupFailure);
			}
		}
		catch (Exception ex)
		{
			failures.Add(LiveFailureClassifier.Classify(ex, LiveFailureClassifier.CleanupStep, cleanupToken));
		}

		if (failures.Count > 0)
		{
			var message = string.Join(
				" Then: ",
				failures.Select(failure => $"BGG live write failed at step '{failure.Step}': {failure.Kind}. {failure.Detail}"));
			Assert.Fail(redactor.Redact(message));
		}
	}

	private static bool IsOptedIn() =>
		string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "true", StringComparison.OrdinalIgnoreCase);

	private static LiveGameEntry CheckGuards(LiveBggSettings settings, SecretRedactor redactor)
	{
		var missing = settings.MissingVariables();
		if (missing.Count > 0)
		{
			Assert.Fail($"The live BGG write test needs these environment variables: {string.Join(", ", missing)}.");
		}

		var allowlist = LiveTestAllowlist.Load(AppContext.BaseDirectory);
		if (allowlist is null || allowlist.Count == 0)
		{
			Assert.Fail($"The live BGG write test refuses to run: {LiveTestAllowlist.FileName} is missing or holds no usernames.");
		}

		if (!allowlist.Contains(settings.Username))
		{
			Assert.Fail(redactor.Redact($"The live BGG write test refuses to run: the BGG username '{settings.Username}' is not in {LiveTestAllowlist.FileName}."));
		}

		var entry = LiveGameDataSet.Load(AppContext.BaseDirectory)?.Find(LiveBggId);
		if (entry is null || !entry.LiveWritable)
		{
			Assert.Fail($"The live BGG write test refuses to run: {LiveGameDataSet.FileName} has no liveWritable entry with id {LiveBggId}.");
		}

		return entry;
	}

	private static SocketsHttpHandler CreateHandler() => new() { UseCookies = false, AllowAutoRedirect = false };

	private static HttpClient CreateHttpClient(HttpMessageHandler handler) =>
		new(handler, disposeHandler: false) { BaseAddress = _bggAddress, Timeout = TimeSpan.FromSeconds(30) };

	private static async Task RemoveAllAsync(BggWriteHttpClient client, IReadOnlyList<long> collIds, CancellationToken cancellationToken)
	{
		foreach (var collId in collIds)
		{
			await client.RemoveFromCollectionAsync(collId, cancellationToken);
		}
	}

	private static async Task<bool> WaitUntilGoneAsync(LiveBggCollectionReader reader, CancellationToken cancellationToken)
	{
		for (var listing = 1; listing <= PollListings; listing++)
		{
			if (listing > 1)
			{
				await Task.Delay(_pollInterval, cancellationToken);
			}

			if ((await reader.ListCollectionItemIdsAsync(LiveBggId, cancellationToken)).Count == 0)
			{
				return true;
			}
		}

		return false;
	}

	private static async Task<LiveFailure?> RemoveListedOnceAsync(
		LiveBggCollectionReader reader,
		BggWriteHttpClient client,
		CancellationToken cancellationToken)
	{
		await RemoveAllAsync(client, await reader.ListCollectionItemIdsAsync(LiveBggId, cancellationToken), cancellationToken);
		return null;
	}

	// BGG can list an added item late, so an empty listing does not end the sweep.
	private static async Task<LiveFailure?> SweepForLateListingAsync(
		LiveBggCollectionReader reader,
		BggWriteHttpClient client,
		CancellationToken cancellationToken)
	{
		IReadOnlyList<long> last = [];
		for (var listing = 1; listing <= PollListings; listing++)
		{
			if (listing > 1)
			{
				await Task.Delay(_pollInterval, cancellationToken);
			}

			last = await reader.ListCollectionItemIdsAsync(LiveBggId, cancellationToken);
			await RemoveAllAsync(client, last, cancellationToken);
		}

		if (last.Count == 0)
		{
			return null;
		}

		for (var confirming = 1; confirming <= ConfirmingListings; confirming++)
		{
			await Task.Delay(_pollInterval, cancellationToken);
			var listed = await reader.ListCollectionItemIdsAsync(LiveBggId, cancellationToken);
			if (listed.Count == 0)
			{
				return null;
			}

			await RemoveAllAsync(client, listed, cancellationToken);
		}

		return new LiveFailure(LiveFailureClassifier.CleanupStep, LiveFailureKind.StillListed, "The game was still listed after the cleanup.");
	}
}
