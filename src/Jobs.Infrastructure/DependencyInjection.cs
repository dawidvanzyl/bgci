using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Jobs.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddJobsInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var connectionString = BuildConnectionString(configuration);

		services.AddHangfire(config => config
			.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
			.UseSimpleAssemblyNameTypeSerializer()
			.UseRecommendedSerializerSettings()
			.UsePostgreSqlStorage(
				options => options.UseNpgsqlConnection(connectionString),
				new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = true, SchemaName = "hangfire" })
			.UseFilter(new JobHistoryRetentionFilter()));

		services.AddHangfireServer(options =>
		{
			options.Queues = [BggQueue.Name];
			options.WorkerCount = 1;
		});

		services.AddAntiforgery();

		return services;
	}

	public static IApplicationBuilder UseJobsDashboard(this IApplicationBuilder app)
	{
		return app.UseHangfireDashboard(
			string.Empty,
			new DashboardOptions { Authorization = [new DashboardAccessFilter()], AppPath = null });
	}

	private static string BuildConnectionString(IConfiguration configuration)
	{
		var port = int.TryParse(Require(configuration, "JobsDatabase:Port"), out var parsedPort)
			? parsedPort
			: throw new InvalidOperationException("The configuration key 'JobsDatabase:Port' must be a number.");

		return new NpgsqlConnectionStringBuilder
		{
			Host = Require(configuration, "JobsDatabase:Host"),
			Port = port,
			Database = Require(configuration, "JobsDatabase:Name"),
			Username = Require(configuration, "JobsDatabase:User"),
			Password = Require(configuration, "JobsDatabase:Password")
		}.ConnectionString;
	}

	private static string Require(IConfiguration configuration, string key)
	{
		var value = configuration[key];
		return string.IsNullOrWhiteSpace(value)
			? throw new InvalidOperationException($"The configuration key '{key}' is missing or empty.")
			: value;
	}
}