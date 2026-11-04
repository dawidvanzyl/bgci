namespace GameCollection.Infrastructure.Postgres.Migrations;

public sealed class DatabaseMigrationException : Exception
{
	public DatabaseMigrationException(string scriptName, Exception innerException)
		: base($"Database migration script '{scriptName}' failed: {innerException.Message}", innerException)
	{
		ScriptName = scriptName;
	}

	public string ScriptName { get; }
}