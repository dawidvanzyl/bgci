using Shared.Persistence.Migrations;

namespace GameCollection.Infrastructure.Tests.Postgres.Migrations;

internal static class EmbeddedMigrationScripts
{
	public static MigrationScripts Load() =>
		MigrationScripts.LoadEmbedded(typeof(DependencyInjection).Assembly, "GameCollection.Infrastructure.Postgres.Scripts.");
}
