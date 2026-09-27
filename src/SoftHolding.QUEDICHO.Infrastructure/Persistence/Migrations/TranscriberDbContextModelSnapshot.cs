using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SoftHolding.QUEDICHO.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TranscriberDbContext))]
public sealed class TranscriberDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "9.0.20")
            .HasAnnotation("Relational:MaxIdentifierLength", 64);

        modelBuilder.Entity("SoftHolding.QUEDICHO.Domain.Sessions.TranscriptionSession", entity =>
        {
            entity.Property<Guid>("Id").HasColumnType("TEXT");
            entity.Property<DateTimeOffset?>("EndedAtUtc").HasColumnType("TEXT");
            entity.Property<string>("FailureReason").HasMaxLength(2000).HasColumnType("TEXT");
            entity.Property<string>("Language").IsRequired().HasMaxLength(16).HasColumnType("TEXT");
            entity.Property<string>("OutputDeviceId").IsRequired().HasMaxLength(500).HasColumnType("TEXT");
            entity.Property<string>("OutputDeviceName").IsRequired().HasMaxLength(300).HasColumnType("TEXT");
            entity.Property<DateTimeOffset>("StartedAtUtc").HasColumnType("TEXT");
            entity.Property<string>("Status").IsRequired().HasMaxLength(32).HasColumnType("TEXT");
            entity.Property<string>("Title").IsRequired().HasMaxLength(300).HasColumnType("TEXT");
            entity.Property<string>("TranscriptionEngine").IsRequired().HasMaxLength(100).HasColumnType("TEXT");
            entity.HasKey("Id");
            entity.HasIndex("StartedAtUtc");
            entity.ToTable("Sessions");
        });

        modelBuilder.Entity("SoftHolding.QUEDICHO.Domain.Transcripts.TranscriptSegment", entity =>
        {
            entity.Property<Guid>("Id").HasColumnType("TEXT");
            entity.Property<double?>("Confidence").HasColumnType("REAL");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("TEXT");
            entity.Property<TimeSpan>("EndTime").HasColumnType("TEXT");
            entity.Property<bool>("IsFinal").HasColumnType("INTEGER");
            entity.Property<Guid>("SessionId").HasColumnType("TEXT");
            entity.Property<Guid?>("SpeakerId").HasColumnType("TEXT");
            entity.Property<string>("SourceKind").IsRequired().HasMaxLength(32).HasColumnType("TEXT");
            entity.Property<TimeSpan>("StartTime").HasColumnType("TEXT");
            entity.Property<string>("Text").IsRequired().HasColumnType("TEXT");
            entity.HasKey("Id");
            entity.HasIndex("SessionId", "StartTime");
            entity.ToTable("TranscriptSegments");
        });

        modelBuilder.Entity("SoftHolding.QUEDICHO.Domain.Transcripts.TranscriptSegment", entity =>
        {
            entity.HasOne("SoftHolding.QUEDICHO.Domain.Sessions.TranscriptionSession", null)
                .WithMany()
                .HasForeignKey("SessionId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
    }
}
