namespace SoftHolding.QUEDICHO.Domain.Sessions;

public sealed class TranscriptionSession
{
    private TranscriptionSession()
    {
    }

    private TranscriptionSession(
        Guid id,
        string title,
        string language,
        string transcriptionEngine,
        string outputDeviceId,
        string outputDeviceName,
        DateTimeOffset startedAtUtc)
    {
        Id = id;
        Title = title;
        Language = language;
        TranscriptionEngine = transcriptionEngine;
        OutputDeviceId = outputDeviceId;
        OutputDeviceName = outputDeviceName;
        StartedAtUtc = startedAtUtc;
        Status = SessionStatus.Starting;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public string TranscriptionEngine { get; private set; } = string.Empty;
    public string OutputDeviceId { get; private set; } = string.Empty;
    public string OutputDeviceName { get; private set; } = string.Empty;
    public SessionStatus Status { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? EndedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }

    public static TranscriptionSession Create(
        string title,
        string language,
        string transcriptionEngine,
        string outputDeviceId,
        string outputDeviceName,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(transcriptionEngine);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDeviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDeviceName);

        return new TranscriptionSession(
            Guid.NewGuid(),
            title.Trim(),
            language.Trim(),
            transcriptionEngine.Trim(),
            outputDeviceId,
            outputDeviceName,
            nowUtc);
    }

    public void MarkListening()
    {
        EnsureStatus(SessionStatus.Starting, SessionStatus.Paused);
        Status = SessionStatus.Listening;
    }

    public void Pause()
    {
        EnsureStatus(SessionStatus.Listening);
        Status = SessionStatus.Paused;
    }

    public void BeginStopping()
    {
        EnsureStatus(SessionStatus.Listening, SessionStatus.Paused, SessionStatus.Starting);
        Status = SessionStatus.Stopping;
    }

    public void Complete(DateTimeOffset nowUtc)
    {
        EnsureStatus(SessionStatus.Stopping, SessionStatus.Listening, SessionStatus.Paused);
        Status = SessionStatus.Completed;
        EndedAtUtc = nowUtc;
    }

    public void Interrupt(DateTimeOffset nowUtc)
    {
        if (Status is SessionStatus.Completed or SessionStatus.Faulted)
        {
            return;
        }

        Status = SessionStatus.Interrupted;
        EndedAtUtc = nowUtc;
    }

    public void Fail(string reason, DateTimeOffset nowUtc)
    {
        Status = SessionStatus.Faulted;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Unknown failure" : reason.Trim();
        EndedAtUtc = nowUtc;
    }

    private void EnsureStatus(params SessionStatus[] expected)
    {
        if (!expected.Contains(Status))
        {
            throw new InvalidOperationException(
                $"Cannot transition session {Id} from {Status}. Expected: {string.Join(", ", expected)}.");
        }
    }
}
