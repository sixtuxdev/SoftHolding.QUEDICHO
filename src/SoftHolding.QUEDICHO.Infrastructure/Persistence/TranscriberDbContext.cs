using Microsoft.EntityFrameworkCore;
using SoftHolding.QUEDICHO.Domain.Sessions;
using SoftHolding.QUEDICHO.Domain.Transcripts;

namespace SoftHolding.QUEDICHO.Infrastructure.Persistence;

public sealed class TranscriberDbContext(DbContextOptions<TranscriberDbContext> options) : DbContext(options)
{
    public DbSet<TranscriptionSession> Sessions => Set<TranscriptionSession>();
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var session = modelBuilder.Entity<TranscriptionSession>();
        session.ToTable("Sessions");
        session.HasKey(entity => entity.Id);
        session.Property(entity => entity.Title).HasMaxLength(300).IsRequired();
        session.Property(entity => entity.Language).HasMaxLength(16).IsRequired();
        session.Property(entity => entity.TranscriptionEngine).HasMaxLength(100).IsRequired();
        session.Property(entity => entity.OutputDeviceId).HasMaxLength(500).IsRequired();
        session.Property(entity => entity.OutputDeviceName).HasMaxLength(300).IsRequired();
        session.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        session.Property(entity => entity.FailureReason).HasMaxLength(2000);
        session.HasIndex(entity => entity.StartedAtUtc);

        var segment = modelBuilder.Entity<TranscriptSegment>();
        segment.ToTable("TranscriptSegments");
        segment.HasKey(entity => entity.Id);
        segment.Property(entity => entity.SourceKind).HasConversion<string>().HasMaxLength(32).IsRequired();
        segment.Property(entity => entity.Text).IsRequired();
        segment.Property(entity => entity.Confidence);
        segment.HasIndex(entity => new { entity.SessionId, entity.StartTime });
        segment.HasOne<TranscriptionSession>()
            .WithMany()
            .HasForeignKey(entity => entity.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
