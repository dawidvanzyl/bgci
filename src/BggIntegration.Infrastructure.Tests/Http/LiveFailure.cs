namespace BggIntegration.Infrastructure.Tests.Http;

public sealed record LiveFailure(string Step, LiveFailureKind Kind, string Detail);