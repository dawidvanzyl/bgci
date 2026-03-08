using GameCollection.Domain.ValueObjects;

namespace GameCollection.Domain.Events;

public record GameAddedToCollection(GameId GameId, GameName GameName);
