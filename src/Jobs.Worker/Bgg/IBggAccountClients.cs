namespace Jobs.Worker.Bgg;

public interface IBggAccountClients
{
	BggAccountClient Get(string account);
}