namespace Serval.Translation.Models;

[TestFixture]
public class EngineModelTests
{
    [Test]
    [TestCase("PascalCase", "PascalCase")]
    [TestCase("snakeCase", "SnakeCase")]
    [TestCase("kebab-case", "KebabCase")]
    [TestCase("space case", "SpaceCase")]
    [TestCase("underscore_case", "UnderscoreCase")]
    public void PascalCaseTests(string input, string output)
    {
        Assert.That(input.ToPascalCase(), Is.EqualTo(output));
    }
}
