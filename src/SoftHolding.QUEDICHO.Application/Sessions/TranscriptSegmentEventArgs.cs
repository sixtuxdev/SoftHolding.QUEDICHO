using SoftHolding.QUEDICHO.Domain.Transcripts;

namespace SoftHolding.QUEDICHO.Application.Sessions;

public sealed class TranscriptSegmentEventArgs(TranscriptSegment segment) : EventArgs
{
    public TranscriptSegment Segment { get; } = segment;
}
