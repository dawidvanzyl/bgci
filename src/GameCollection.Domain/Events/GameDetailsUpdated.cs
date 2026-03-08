using GameCollection.Domain.ValueObjects;

namespace GameCollection.Domain.Events;

public record GameDetailsUpdated(GameId GameId, GameName GameName);
