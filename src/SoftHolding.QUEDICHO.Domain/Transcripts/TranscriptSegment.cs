using SoftHolding.QUEDICHO.Domain.Sessions;

namespace SoftHolding.QUEDICHO.Domain.Transcripts;

public sealed class TranscriptSegment
{
    private TranscriptSegment()
    {
    }

    private TranscriptSegment(
        Guid id,
        Guid sessionId,
        AudioSourceKind sourceKind,
        string text,
        TimeSpan startTime,
        TimeSpan endTime,
        double? confidence,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        SessionId = sessionId;
        SourceKind = sourceKind;
        Text = text;
        StartTime = startTime;
        EndTime = endTime;
        Confidence = confidence;
        CreatedAtUtc = createdAtUtc;
        IsFinal = true;
    }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public AudioSourceKind SourceKind { get; private set; }
    public Guid? SpeakerId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }
    public double? Confidence { get; private set; }
    public bool IsFinal { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static TranscriptSegment Create(
        Guid sessionId,
        AudioSourceKind sourceKind,
        string text,
        TimeSpan startTime,
        TimeSpan endTime,
        double? confidence,
        DateTimeOffset nowUtc)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("A session id is required.", nameof(sessionId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (startTime < TimeSpan.Zero || endTime < startTime)
        {
            throw new ArgumentOutOfRangeException(nameof(endTime), "Segment times are invalid.");
        }

        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");
        }

        return new TranscriptSegment(
            Guid.NewGuid(),
            sessionId,
            sourceKind,
            text.Trim(),
            startTime,
            endTime,
            confidence,
            nowUtc);
    }
}
