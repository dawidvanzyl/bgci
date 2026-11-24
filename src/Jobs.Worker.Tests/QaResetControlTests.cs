using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Shouldly;
using System.Net;
using Xunit;

namespace Jobs.Worker.Tests;

public sealed class QaResetControlTests
{
	private readonly Mock<IJobsReset> _reset = new();

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task Post_ControlSwitchedOff_AnswersNotFoundAndNeverResets()
	{
		using var host = await StartAsync(enabled: false);

		var response = await host.GetTestClient().PostAsync("/qa/reset", null, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		_reset.Verify(r => r.ResetAsync(It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task Post_ControlSwitchedOn_ResetsOnceAndAnswersNoContent()
	{
		using var host = await StartAsync(enabled: true);

		var response = await host.GetTestClient().PostAsync("/qa/reset", null, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		_reset.Verify(r => r.ResetAsync(It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task Get_ControlSwitchedOn_AnswersNotFoundAndNeverResets()
	{
		using var host = await StartAsync(enabled: true);

		var response = await host.GetTestClient().GetAsync("/qa/reset", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		_reset.Verify(r => r.ResetAsync(It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task Post_ResetTimesOut_DoesNotHideTheTimeout()
	{
		_reset.Setup(r => r.ResetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("a job is still running"));
		using var host = await StartAsync(enabled: true);

		await Should.ThrowAsync<TimeoutException>(
			() => host.GetTestClient().PostAsync("/qa/reset", null, TestContext.Current.CancellationToken));
	}

	private async Task<IHost> StartAsync(bool enabled)
	{
		return await new HostBuilder()
			.ConfigureWebHost(web => web
				.UseTestServer()
				.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
					new Dictionary<string, string?> { ["Jobs:QaResetEnabled"] = enabled ? "true" : "false" }))
				.ConfigureServices(services => services.AddSingleton(_reset.Object))
				.Configure(app => app.UseQaResetControl()))
			.StartAsync(TestContext.Current.CancellationToken);
	}
}
