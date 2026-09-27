using SoftHolding.QUEDICHO.Domain.Sessions;
using SoftHolding.QUEDICHO.Domain.Transcripts;

namespace SoftHolding.QUEDICHO.Application.Persistence;

public interface ISessionRepository
{
    Task CreateAsync(TranscriptionSession session, CancellationToken cancellationToken = default);

    Task UpdateAsync(TranscriptionSession session, CancellationToken cancellationToken = default);

    Task AddSegmentAsync(TranscriptSegment segment, CancellationToken cancellationToken = default);

    Task<int> MarkUnfinishedSessionsInterruptedAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}
