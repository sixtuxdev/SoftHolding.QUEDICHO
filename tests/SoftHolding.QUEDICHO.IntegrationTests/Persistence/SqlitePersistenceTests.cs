using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using SoftHolding.QUEDICHO.Domain.Sessions;
using SoftHolding.QUEDICHO.Domain.Transcripts;
using SoftHolding.QUEDICHO.Infrastructure.Persistence;

namespace SoftHolding.QUEDICHO.IntegrationTests.Persistence;

public sealed class SqlitePersistenceTests
{
    [Fact]
    public async Task RepositoryPersistsSessionAndSegment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var databasePath = Path.Combine(Path.GetTempPath(), $"quedicho-test-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<TranscriberDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            var factory = new TestDbContextFactory(options);
            await using (var setup = await factory.CreateDbContextAsync(cancellationToken))
            {
                await setup.Database.EnsureCreatedAsync(cancellationToken);
            }

            var repository = new SessionRepository(factory, NullLogger<SessionRepository>.Instance);
            var now = DateTimeOffset.UtcNow;
            var session = TranscriptionSession.Create(
                "Prueba",
                "es",
                "Whisper Local",
                "device-1",
                "Headphones",
                now);
            await repository.CreateAsync(session, cancellationToken);
            var segment = TranscriptSegment.Create(
                session.Id,
                AudioSourceKind.SystemAudio,
                "Buenos días",
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                0.9,
                now);
            await repository.AddSegmentAsync(segment, cancellationToken);

            await using (var verification = await factory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal(1, await verification.Sessions.CountAsync(cancellationToken));
                Assert.Equal("Buenos días", (await verification.TranscriptSegments.SingleAsync(cancellationToken)).Text);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<TranscriberDbContext> options)
        : IDbContextFactory<TranscriberDbContext>
    {
        public TranscriberDbContext CreateDbContext() => new(options);

        public Task<TranscriberDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
