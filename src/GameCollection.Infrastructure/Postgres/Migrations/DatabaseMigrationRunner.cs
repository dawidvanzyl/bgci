using DbUp;
using DbUp.Engine;
using DbUp.Postgresql;

namespace GameCollection.Infrastructure.Postgres.Migrations;

public sealed class DatabaseMigrationRunner
{
	private const string _journalSchema = "public";
	private const string _schemaJournal = "migration_schema_journal";
	private const string _functionJournal = "migration_function_journal";
	private const string _seedJournal = "migration_seed_journal";

	private readonly string _connectionString;
	private readonly MigrationScripts _scripts;

	public DatabaseMigrationRunner(string connectionString, MigrationScripts scripts)
	{
		_connectionString = connectionString;
		_scripts = scripts;
	}

	public Task RunAsync(CancellationToken cancellationToken = default)
	{
		Apply(_scripts.Schema, _schemaJournal, runAlways: false, cancellationToken);
		Apply(_scripts.Functions, _functionJournal, runAlways: true, cancellationToken);
		Apply(_scripts.Seeds, _seedJournal, runAlways: true, cancellationToken);
		return Task.CompletedTask;
	}

	private void Apply(
		IReadOnlyList<MigrationScript> scripts,
		string journalTable,
		bool runAlways,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var sqlScripts = scripts
			.OrderBy(script => script.Name, StringComparer.Ordinal)
			.Select(script => new SqlScript(script.Name, script.Contents))
			.ToList();

		var builder = DeployChanges.To
			.PostgresqlDatabase(_connectionString)
			.WithScripts(sqlScripts)
			.WithTransactionPerScript()
			// PL/pgSQL dollar quotes such as $body$ would otherwise be read as DbUp variables.
			.WithVariablesDisabled()
			.LogToNowhere();

		builder.Configure(config =>
		{
			var journal = new PostgresqlTableJournal(() => config.ConnectionManager, () => config.Log, _journalSchema, journalTable);
			config.Journal = runAlways ? new RunAlwaysJournal(journal) : journal;
		});

		var result = builder.Build().PerformUpgrade();

		if (!result.Successful)
		{
			throw new DatabaseMigrationException(result.ErrorScript?.Name ?? "unknown", result.Error);
		}
	}
}