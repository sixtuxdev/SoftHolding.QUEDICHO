using SoftHolding.QUEDICHO.Application.TextInjection;

namespace SoftHolding.QUEDICHO.UnitTests.TextInjection;

public sealed class TextSegmentSpacingPolicyTests
{
    [Theory]
    [InlineData(null, "Buenos días.", "Buenos días.")]
    [InlineData("Buenos días.", "Vamos a comenzar.", " Vamos a comenzar.")]
    [InlineData("Buenos días. ", "Vamos a comenzar.", "Vamos a comenzar.")]
    [InlineData("Hola", ", ¿cómo estás?", ", ¿cómo estás?")]
    [InlineData("Pregunta (", "respuesta)", "respuesta)")]
    [InlineData("Primera línea\n", "Segunda línea", "Segunda línea")]
    public void PrepareForInsertionUsesExactlyOneRequiredSeparator(
        string? previous,
        string current,
        string expected)
    {
        var result = TextSegmentSpacingPolicy.PrepareForInsertion(previous, current);

        Assert.Equal(expected, result);
    }
}
