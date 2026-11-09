using GameCollection.Application.Abstractions;

namespace GameCollection.Application.DTOs;

public record DeleteGameResult(BggWriteOutcome BggWrite);