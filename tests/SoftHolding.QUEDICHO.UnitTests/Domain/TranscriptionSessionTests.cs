using SoftHolding.QUEDICHO.Domain.Sessions;

namespace SoftHolding.QUEDICHO.UnitTests.Domain;

public sealed class TranscriptionSessionTests
{
    [Fact]
    public void SessionFollowsExpectedLifecycle()
    {
        var startedAt = new DateTimeOffset(2026, 9, 27, 14, 0, 0, TimeSpan.Zero);
        var session = TranscriptionSession.Create(
            "Reunión",
            "es",
            "Whisper Local",
            "device-1",
            "Headphones",
            startedAt);

        session.MarkListening();
        session.Pause();
        session.MarkListening();
        session.BeginStopping();
        session.Complete(startedAt.AddMinutes(10));

        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.Equal(startedAt.AddMinutes(10), session.EndedAtUtc);
    }

    [Fact]
    public void SessionRejectsInvalidTransition()
    {
        var session = TranscriptionSession.Create(
            "Reunión",
            "es",
            "Whisper Local",
            "device-1",
            "Headphones",
            DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(session.Pause);
    }
}
