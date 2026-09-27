using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SoftHolding.QUEDICHO.Application.Persistence;
using SoftHolding.QUEDICHO.Domain.Sessions;
using SoftHolding.QUEDICHO.Domain.Transcripts;

namespace SoftHolding.QUEDICHO.Infrastructure.Persistence;

public sealed class SessionRepository(
    IDbContextFactory<TranscriberDbContext> contextFactory,
    ILogger<SessionRepository> logger) : ISessionRepository
{
    public async Task CreateAsync(TranscriptionSession session, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Sessions.Add(session);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(TranscriptionSession session, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Sessions.Update(session);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddSegmentAsync(TranscriptSegment segment, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.TranscriptSegments.Add(segment);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> MarkUnfinishedSessionsInterruptedAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var unfinished = await context.Sessions
            .Where(session => session.Status == SessionStatus.Starting ||
                              session.Status == SessionStatus.Listening ||
                              session.Status == SessionStatus.Paused ||
                              session.Status == SessionStatus.Stopping)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var session in unfinished)
        {
            session.Interrupt(nowUtc);
        }

        if (unfinished.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            PersistenceLog.SessionsRecovered(logger, unfinished.Count);
        }

        return unfinished.Count;
    }
}
