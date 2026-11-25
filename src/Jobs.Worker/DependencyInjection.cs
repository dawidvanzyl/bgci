using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using Hangfire;
using Jobs.Application.Abstractions;
using Jobs.Infrastructure;
using Jobs.Infrastructure.Jobs;
using Jobs.Worker.Bgg;
using Jobs.Worker.Jobs;
using Jobs.Worker.LiveTest;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Jobs.Worker;

public static class DependencyInjection
{
	private const string _defaultBggBaseAddress = "https://boardgamegeek.com/";
	private const string _qaResetPath = "/qa/reset";
	private const string _liveTestJobId = "bgg-live-test";
	private const string _defaultLiveTestSchedule = "0 3 * * *";

	public static IServiceCollection AddJobsWorker(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var schedulePollingInterval = ReadSeconds(configuration, "Jobs:SchedulePollingIntervalSeconds");

		services.AddHangfireServer(options =>
		{
			options.Queues = [BggQueue.Name];
			options.WorkerCount = 1;
			if (schedulePollingInterval is { } interval)
			{
				options.SchedulePollingInterval = interval;
			}
		});

		services.AddHangfireServer(options =>
		{
			options.ServerName = "bgg-live-test";
			options.Queues = [BggLiveTestQueue.Name];
			options.WorkerCount = 1;
		});

		ReplaceRetryPolicy(configuration);
		AddWaitFilter(ReadSeconds(configuration, "Jobs:WaitForEarlierRequestSeconds") ?? TimeSpan.FromSeconds(15));

		services.AddAntiforgery();

		services.TryAddSingleton(TimeProvider.System);
		services.AddSingleton<IRunningJobs, RunningJobs>();
		services.AddScoped<IBggAddJob, BggAddJob>();
		services.AddScoped<IBggRemoveJob, BggRemoveJob>();
		services.AddScoped<IJobsReset, JobsReset>();
		services.AddScoped<IBggLiveTestJob, BggLiveTestJob>();

		var ownerSettings = configuration.GetSection("Bgg").Get<BggSettings>() ?? new BggSettings();
		var testSettings = configuration.GetSection("BggTestAccount").Get<BggSettings>() ?? new BggSettings();
		var allowlist = BggTestAccountAllowlist.Load(AppContext.BaseDirectory);
		services.AddSingleton(allowlist);
		services.AddSingleton<IBggTestAccount>(new BggTestAccount(ownerSettings, testSettings, allowlist));
		services.AddSingleton<ILiveWritableGames>(LiveWritableGames.Load(AppContext.BaseDirectory));

		var bggBaseAddress = ResolveBggBaseAddress(configuration["Bgg:BaseUrl"]);
		services.AddHttpClient(BggAccountClients.BggWriteClientName, client =>
		{
			client.BaseAddress = bggBaseAddress;
			client.Timeout = TimeSpan.FromSeconds(30);
		})
		.RedactLoggedHeaders(_ => true)
		.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
		{
			UseCookies = false,
			AllowAutoRedirect = false,
		});
		services.AddSingleton<IBggAccountClients>(provider => new BggAccountClients(
			provider.GetRequiredService<IHttpClientFactory>(),
			provider.GetRequiredService<TimeProvider>(),
			provider.GetRequiredService<ILogger<BggWriteHttpClient>>(),
			allowlist,
			ownerSettings,
			testSettings));
		services.AddScoped<IBggCollectionWriter, BggCollectionWriterAdapter>();
		services.AddScoped<IBggTestCollectionReader, BggTestCollectionReader>();

		return services;
	}

	public static IApplicationBuilder UseBggLiveTestSchedule(this IApplicationBuilder app)
	{
		var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
		var recurringJobs = app.ApplicationServices.GetRequiredService<IRecurringJobManager>();
		if (!configuration.GetValue<bool>("LiveTest:Enabled"))
		{
			recurringJobs.RemoveIfExists(_liveTestJobId);
			return app;
		}

		var schedule = configuration["LiveTest:Schedule"];
		var cron = string.IsNullOrWhiteSpace(schedule) ? _defaultLiveTestSchedule : schedule.Trim();
		recurringJobs.AddOrUpdate<IBggLiveTestJob>(
			_liveTestJobId,
			BggLiveTestQueue.Name,
			job => job.RunAsync(null, CancellationToken.None),
			cron,
			new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, MisfireHandling = MisfireHandlingMode.Relaxed });
		return app;
	}

	public static IApplicationBuilder UseQaResetControl(this IApplicationBuilder app)
	{
		var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
		return !configuration.GetValue<bool>("Jobs:QaResetEnabled")
			? app
			: app.UseWhen(
			context => HttpMethods.IsPost(context.Request.Method)
				&& context.Request.Path.Equals(_qaResetPath, StringComparison.OrdinalIgnoreCase),
			branch => branch.Run(async context =>
			{
				await context.RequestServices.GetRequiredService<IJobsReset>().ResetAsync(context.RequestAborted);
				context.Response.StatusCode = StatusCodes.Status204NoContent;
			}));
	}

	public static IApplicationBuilder UseJobsDashboard(this IApplicationBuilder app)
	{
		return app.UseHangfireDashboard(
			string.Empty,
			new DashboardOptions { Authorization = [new DashboardAccessFilter()], AppPath = null });
	}

	private static void ReplaceRetryPolicy(IConfiguration configuration)
	{
		foreach (var filter in GlobalJobFilters.Filters.Where(f => f.Instance is AutomaticRetryAttribute).ToList())
		{
			GlobalJobFilters.Filters.Remove(filter.Instance);
		}

		GlobalJobFilters.Filters.Add(BggRetryPolicy.Create(configuration));
	}

	private static void AddWaitFilter(TimeSpan delay)
	{
		foreach (var filter in GlobalJobFilters.Filters.Where(f => f.Instance is WaitForEarlierRequestFilter).ToList())
		{
			GlobalJobFilters.Filters.Remove(filter.Instance);
		}

		GlobalJobFilters.Filters.Add(new WaitForEarlierRequestFilter(delay));
	}

	private static TimeSpan? ReadSeconds(IConfiguration configuration, string key)
	{
		var value = configuration[key];
		return string.IsNullOrWhiteSpace(value)
			? null
			: double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds > 0
			? TimeSpan.FromSeconds(seconds)
			: throw new InvalidOperationException($"The configuration key '{key}' must be a positive number of seconds.");
	}

	private static Uri ResolveBggBaseAddress(string? configured)
	{
		return string.IsNullOrWhiteSpace(configured)
			? new Uri(_defaultBggBaseAddress)
			: new Uri(configured.Trim().TrimEnd('/') + "/");
	}
}