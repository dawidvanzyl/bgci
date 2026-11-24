using Shared.Persistence.Migrations;

namespace Jobs.Infrastructure.Postgres;

public sealed class JobsDatabaseMigrator
{
	private readonly DatabaseMigrationRunner _runner;

	public JobsDatabaseMigrator(DatabaseMigrationRunner runner)
	{
		_runner = runner;
	}

	public Task RunAsync(CancellationToken cancellationToken = default) => _runner.RunAsync(cancellationToken);
}
