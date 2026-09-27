using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SoftHolding.QUEDICHO.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TranscriberDbContext))]
[Migration("20260927180000_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                Language = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                TranscriptionEngine = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                OutputDeviceId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                OutputDeviceName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                EndedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                FailureReason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Sessions", entity => entity.Id));

        migrationBuilder.CreateTable(
            name: "TranscriptSegments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                SourceKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                SpeakerId = table.Column<Guid>(type: "TEXT", nullable: true),
                Text = table.Column<string>(type: "TEXT", nullable: false),
                StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                EndTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                Confidence = table.Column<double>(type: "REAL", nullable: true),
                IsFinal = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TranscriptSegments", entity => entity.Id);
                table.ForeignKey(
                    name: "FK_TranscriptSegments_Sessions_SessionId",
                    column: entity => entity.SessionId,
                    principalTable: "Sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_StartedAtUtc",
            table: "Sessions",
            column: "StartedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_TranscriptSegments_SessionId_StartTime",
            table: "TranscriptSegments",
            columns: ["SessionId", "StartTime"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TranscriptSegments");
        migrationBuilder.DropTable(name: "Sessions");
    }
}
