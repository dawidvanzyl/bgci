using Testcontainers.PostgreSql;
using Xunit;

namespace GameCollection.Testing.Postgres;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
	private const string _user = "postgres";
	private const string _password = "postgres";

	private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
		.WithUsername(_user)
		.WithPassword(_password)
		.Build();

	public async ValueTask InitializeAsync() => await _container.StartAsync();

	public async ValueTask DisposeAsync() => await _container.DisposeAsync();

	public async Task<string> CreateDatabaseAsync()
	{
		var name = $"test_{Guid.NewGuid():N}";
		var result = await _container.ExecScriptAsync($"CREATE DATABASE {name}");
		if (result.ExitCode != 0)
		{
			throw new InvalidOperationException($"Could not create database '{name}': {result.Stderr}");
		}

		var port = _container.GetMappedPublicPort(PostgreSqlBuilder.PostgreSqlPort);
		return $"Host={_container.Hostname};Port={port};Database={name};Username={_user};Password={_password};Pooling=false";
	}
}