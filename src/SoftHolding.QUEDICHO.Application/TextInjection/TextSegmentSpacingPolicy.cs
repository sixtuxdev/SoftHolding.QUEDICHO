namespace SoftHolding.QUEDICHO.Application.TextInjection;

public static class TextSegmentSpacingPolicy
{
    private const string NoLeadingSpaceCharacters = ".,;:!?%)]}»”’\r\n";

    private const string NoTrailingSpaceCharacters = "([{¿¡«“‘\r\n";

    public static string PrepareForInsertion(string? previousSegment, string currentSegment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSegment);

        var current = currentSegment.Trim();
        if (string.IsNullOrWhiteSpace(previousSegment) ||
            char.IsWhiteSpace(currentSegment[0]) ||
            NoLeadingSpaceCharacters.Contains(current[0]))
        {
            return current;
        }

        var previous = previousSegment.TrimEnd();
        if (previous.Length == 0 ||
            char.IsWhiteSpace(previousSegment[^1]) ||
            NoTrailingSpaceCharacters.Contains(previous[^1]))
        {
            return current;
        }

        return $" {current}";
    }
}
