using Dapper;

namespace GameCollection.Infrastructure.Postgres;

public static class PostgresClientSettings
{
	// Npgsql reads the compatibility switch once, on first use, so this must run before any Npgsql call in the process.
	public static void Apply()
	{
		AppContext.SetSwitch("Npgsql.EnableStoredProcedureCompatMode", true);
		DefaultTypeMap.MatchNamesWithUnderscores = true;
	}
}