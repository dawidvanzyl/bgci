using GameCollection.Testing.Postgres;
using Jobs.Infrastructure.Tests;
using Xunit;

[assembly: AssemblyFixture(typeof(PostgresClientSettingsFixture))]
[assembly: AssemblyFixture(typeof(PostgresContainerFixture))]