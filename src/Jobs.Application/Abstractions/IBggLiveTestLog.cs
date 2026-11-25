using Jobs.Application.LiveTest;

namespace Jobs.Application.Abstractions;

public interface IBggLiveTestLog
{
	Task RecordAsync(BggLiveTestRecord record, CancellationToken cancellationToken = default);
}