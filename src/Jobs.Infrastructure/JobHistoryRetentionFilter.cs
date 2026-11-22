using Hangfire.States;

namespace Jobs.Infrastructure;

public sealed class JobHistoryRetentionFilter : Hangfire.Common.JobFilterAttribute, IApplyStateFilter
{
	public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

	public void OnStateApplied(ApplyStateContext context, Hangfire.Storage.IWriteOnlyTransaction transaction)
	{
		context.JobExpirationTimeout = Retention;
	}

	public void OnStateUnapplied(ApplyStateContext context, Hangfire.Storage.IWriteOnlyTransaction transaction)
	{
	}
}