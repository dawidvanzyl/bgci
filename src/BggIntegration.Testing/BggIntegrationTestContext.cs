using BggIntegration.Domain.Interfaces;
using Moq;

namespace BggIntegration.Testing;

/// <summary>
/// One isolated set of fakes and one service provider per test. Tests configure the mocks,
/// then resolve the system under test from <see cref="ServiceProvider"/>.
/// </summary>
public sealed class BggIntegrationTestContext
{
	public BggIntegrationTestContext(Func<BggIntegrationTestContext, IServiceProvider> serviceProviderFactory)
	{
		ServiceProvider = serviceProviderFactory(this)
			?? throw new ArgumentNullException(nameof(serviceProviderFactory));
	}

	public ClientMocks Clients { get; } = new();

	public IServiceProvider ServiceProvider { get; }

	public sealed class ClientMocks
	{
		public Mock<IBggClient> BggClientMock { get; } = new();
	}
}