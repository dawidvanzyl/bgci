using BggIntegration.Domain.Models;
using GameCollection.Application.Commands;

namespace BggIntegration.Domain.Interfaces;

/// <summary>
/// Anti-Corruption Layer: translates BGG domain concepts into GameCollection domain commands.
/// </summary>
public interface IBggTranslator
{
    AddGameFromBggCommand ToAddGameCommand(BggGameDetails details);
}
