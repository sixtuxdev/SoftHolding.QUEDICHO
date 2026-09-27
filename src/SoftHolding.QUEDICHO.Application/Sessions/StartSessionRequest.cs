namespace SoftHolding.QUEDICHO.Application.Sessions;

public sealed record StartSessionRequest(string? DeviceId, string Language = "es", string? Title = null);
