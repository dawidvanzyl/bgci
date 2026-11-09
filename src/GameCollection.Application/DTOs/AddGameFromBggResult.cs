using GameCollection.Application.Abstractions;

namespace GameCollection.Application.DTOs;

public record AddGameFromBggResult(Guid Id, BggWriteOutcome BggWrite);