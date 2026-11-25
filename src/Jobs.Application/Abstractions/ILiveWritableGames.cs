using Jobs.Application.LiveTest;

namespace Jobs.Application.Abstractions;

public interface ILiveWritableGames
{
	LiveTestGame? Find(int bggId);
}