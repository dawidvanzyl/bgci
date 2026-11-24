namespace Jobs.Application;

public static class BggJobPolicy
{
	public const int MaxAttempts = 3;

	public static bool IsFinalAttempt(int retryCount) => retryCount >= MaxAttempts - 1;
}