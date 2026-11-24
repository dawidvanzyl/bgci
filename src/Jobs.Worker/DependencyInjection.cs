using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using Hangfire;
using Jobs.Application.Abstractions;
using Jobs.Infrastructure.Jobs;
using Jobs.Worker.Bgg;
using Jobs.Worker.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Jobs.Infrastructure;

namespace Jobs.Worker;

public static class DependencyInjection
{
	private const string _defaultBggBaseAddress = "https://boardgamegeek.com/";
	private const string _qaResetPath = "/qa/reset";

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

		ReplaceRetryPolicy(configuration);

		services.AddAntiforgery();

		services.TryAddSingleton(TimeProvider.System);
		services.AddSingleton<IRunningJobs, RunningJobs>();
		services.AddScoped<IBggAddJob, BggAddJob>();
		services.AddScoped<IBggRemoveJob, BggRemoveJob>();
		services.AddScoped<IJobsReset, JobsReset>();

		services.Configure<BggSettings>(configuration.GetSection("Bgg"));
		var bggBaseAddress = ResolveBggBaseAddress(configuration["Bgg:BaseUrl"]);
		services.AddHttpClient<IBggWriteClient, BggWriteHttpClient>(client =>
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
		services.AddSingleton<BggSession>();
		services.AddScoped<IBggCollectionWriter, BggCollectionWriterAdapter>();

		return services;
	}

	public static IApplicationBuilder UseQaResetControl(this IApplicationBuilder app)
	{
		var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
		if (!configuration.GetValue<bool>("Jobs:QaResetEnabled"))
		{
			return app;
		}

		return app.UseWhen(
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

	private static TimeSpan? ReadSeconds(IConfiguration configuration, string key)
	{
		var value = configuration[key];
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		return double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds > 0
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
