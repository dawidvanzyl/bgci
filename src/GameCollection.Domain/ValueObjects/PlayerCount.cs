namespace GameCollection.Domain.ValueObjects;

public record PlayerCount
{
    public int Min { get; }
    public int Max { get; }

    public PlayerCount(int min, int max)
    {
        if (min < 1) throw new ArgumentException("Minimum player count must be at least 1.", nameof(min));
        if (max < min) throw new ArgumentException("Maximum player count cannot be less than minimum.", nameof(max));
        Min = min;
        Max = max;
    }

    public override string ToString() => Min == Max ? $"{Min}" : $"{Min}–{Max}";
}
