namespace GameCollection.Domain.ValueObjects;

public record GameId(Guid Value)
{
    public static GameId New() => new(Guid.NewGuid());
    public static GameId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
