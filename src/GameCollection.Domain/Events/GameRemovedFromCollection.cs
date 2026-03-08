using GameCollection.Domain.ValueObjects;

namespace GameCollection.Domain.Events;

public record GameRemovedFromCollection(GameId GameId);
