namespace Jobs.Application.Abstractions;

public interface IBggTestAccount
{
	string? RefusalReason();

	string Redact(string text);
}