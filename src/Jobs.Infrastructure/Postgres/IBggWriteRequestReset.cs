namespace Jobs.Infrastructure.Postgres;

public interface IBggWriteRequestReset
{
	Task ResetAsync(CancellationToken cancellationToken = default);
}