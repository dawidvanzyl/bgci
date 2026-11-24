using GameCollection.Infrastructure.Postgres;

namespace Jobs.Infrastructure.Tests;

public sealed class PostgresClientSettingsFixture
{
	public PostgresClientSettingsFixture()
	{
		PostgresClientSettings.Apply();
	}
}