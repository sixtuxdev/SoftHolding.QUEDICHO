using Microsoft.Extensions.Logging;

namespace SoftHolding.QUEDICHO.Infrastructure.Persistence;

internal static partial class PersistenceLog
{
    [LoggerMessage(4001, LogLevel.Warning, "Recovered {SessionCount} unfinished transcription sessions")]
    public static partial void SessionsRecovered(ILogger logger, int sessionCount);
}
