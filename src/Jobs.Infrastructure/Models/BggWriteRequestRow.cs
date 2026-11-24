namespace Jobs.Infrastructure.Models;

internal sealed class BggWriteRequestRow
{
	public Guid Id { get; set; }
	public string Kind { get; set; } = default!;
	public int BggId { get; set; }
	public Guid GameId { get; set; }
	public string Name { get; set; } = default!;
	public long? CollectionItemId { get; set; }
	public string Account { get; set; } = default!;
	public string Status { get; set; } = default!;
	public int Attempts { get; set; }
}