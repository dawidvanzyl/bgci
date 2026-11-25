using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;

namespace Jobs.Worker.Bgg;

public sealed record BggAccountClient(IBggWriteClient Client, BggSettings Settings);