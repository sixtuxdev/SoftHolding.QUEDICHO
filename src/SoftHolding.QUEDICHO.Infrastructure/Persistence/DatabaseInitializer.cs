using Microsoft.EntityFrameworkCore;
using SoftHolding.QUEDICHO.Application.Persistence;

namespace SoftHolding.QUEDICHO.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    IDbContextFactory<TranscriberDbContext> contextFactory,
    ISessionRepository sessionRepository,
    TimeProvider timeProvider)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", cancellationToken).ConfigureAwait(false);
        await sessionRepository
            .MarkUnfinishedSessionsInterruptedAsync(timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
    }
}
