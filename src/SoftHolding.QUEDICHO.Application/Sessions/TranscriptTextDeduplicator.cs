namespace SoftHolding.QUEDICHO.Application.Sessions;

public static class TranscriptTextDeduplicator
{
    private const int MaximumComparedWords = 12;

    public static string RemoveRepeatedPrefix(string? previousText, string currentText)
    {
        var currentWords = SplitWords(currentText);
        if (currentWords.Length == 0 || string.IsNullOrWhiteSpace(previousText))
        {
            return currentText.Trim();
        }

        var previousWords = SplitWords(previousText);
        var maximumOverlap = Math.Min(
            MaximumComparedWords,
            Math.Min(previousWords.Length, currentWords.Length));

        for (var overlap = maximumOverlap; overlap > 0; overlap--)
        {
            var previousStart = previousWords.Length - overlap;
            var matches = true;
            for (var index = 0; index < overlap; index++)
            {
                if (!string.Equals(
                        Normalize(previousWords[previousStart + index]),
                        Normalize(currentWords[index]),
                        StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return string.Join(' ', currentWords.Skip(overlap));
            }
        }

        return currentText.Trim();
    }

    private static string[] SplitWords(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Normalize(string word) =>
        new(word.Where(char.IsLetterOrDigit).ToArray());
}
