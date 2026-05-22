using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Persistence.Repositories;

public interface IEventRepository
{
    Task<JoyEvent> AppendAsync(
        Guid correlationId,
        EventSource source,
        string type,
        string payloadJson,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JoyEvent>> QueryAsync(
        long? sinceCursor = null,
        Guid? correlationId = null,
        IReadOnlyList<string>? types = null,
        int limit = 200,
        CancellationToken cancellationToken = default);
}
