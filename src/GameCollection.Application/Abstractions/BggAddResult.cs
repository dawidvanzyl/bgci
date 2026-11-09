namespace GameCollection.Application.Abstractions;

public record BggAddResult(BggWriteOutcome Outcome, long? BggCollId);