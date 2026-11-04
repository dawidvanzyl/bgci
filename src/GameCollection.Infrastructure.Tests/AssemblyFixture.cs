using GameCollection.Infrastructure.Tests;
using GameCollection.Testing.Postgres;
using Xunit;

[assembly: AssemblyFixture(typeof(PostgresClientSettingsFixture))]
[assembly: AssemblyFixture(typeof(PostgresContainerFixture))]