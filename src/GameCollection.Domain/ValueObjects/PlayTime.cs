namespace GameCollection.Domain.ValueObjects;

public record PlayTime
{
    public int Minutes { get; }

    public PlayTime(int minutes)
    {
        if (minutes < 0)
		{
			throw new ArgumentException("Play time cannot be negative.", nameof(minutes));
		}

		Minutes = minutes;
    }

    public override string ToString() => $"{Minutes} min";
}
