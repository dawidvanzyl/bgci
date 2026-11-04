using DbUp.Engine;
using System.Data;

namespace GameCollection.Infrastructure.Postgres.Migrations;

internal sealed class RunAlwaysJournal : IJournal
{
	private readonly IJournal _inner;

	public RunAlwaysJournal(IJournal inner)
	{
		_inner = inner;
	}

	// Reporting nothing as executed makes DbUp run every script on every run, while each run is still recorded.
	public string[] GetExecutedScripts() => [];

	public void StoreExecutedScript(SqlScript script, Func<IDbCommand> dbCommandFactory) =>
		_inner.StoreExecutedScript(script, dbCommandFactory);

	public void EnsureTableExistsAndIsLatestVersion(Func<IDbCommand> dbCommandFactory) =>
		_inner.EnsureTableExistsAndIsLatestVersion(dbCommandFactory);
}