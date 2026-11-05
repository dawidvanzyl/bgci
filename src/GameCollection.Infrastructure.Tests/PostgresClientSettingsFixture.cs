using GameCollection.Infrastructure.Postgres;

namespace GameCollection.Infrastructure.Tests;

public sealed class PostgresClientSettingsFixture
{
	public PostgresClientSettingsFixture()
	{
		PostgresClientSettings.Apply();
	}
}