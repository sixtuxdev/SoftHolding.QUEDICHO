using SoftHolding.QUEDICHO.Application.Sessions;

namespace SoftHolding.QUEDICHO.UnitTests.Sessions;

public sealed class TranscriptTextDeduplicatorTests
{
    [Theory]
    [InlineData("Hola, cómo", "cómo estás", "estás")]
    [InlineData("una prueba de audio", "prueba de audio en vivo", "en vivo")]
    [InlineData("texto anterior", "contenido nuevo", "contenido nuevo")]
    [InlineData("¿Me escuchas?", "Me escuchas", "")]
    public void RemoveRepeatedPrefixReturnsOnlyNewWords(
        string previousText,
        string currentText,
        string expected)
    {
        var result = TranscriptTextDeduplicator.RemoveRepeatedPrefix(previousText, currentText);

        Assert.Equal(expected, result);
    }
}
