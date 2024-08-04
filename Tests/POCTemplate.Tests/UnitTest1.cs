using FluentAssertions;

namespace POCTemplate.Tests;

public class UnitTest1
{
    /// <summary>
    /// Tests the functionality of a specific feature or method.
    /// </summary>
    /// <remarks>
    /// This test method, named <c>Test1</c>, is designed to verify that a boolean value evaluates to the expected result.
    /// It uses the Arrange-Act-Assert pattern, which is a common practice in unit testing.
    /// In the Arrange phase, the expected value is set to <c>true</c>.
    /// In the Act phase, a boolean variable <c>value</c> is assigned the value <c>true</c> as well.
    /// Finally, in the Assert phase, the test checks if <c>value</c> matches the expected value using Fluent Assertions.
    /// This ensures that the functionality being tested behaves as intended.
    /// </remarks>
    [Fact]
    public void Test1()
    {
        // Arrange
        const bool expected = true;

        // Act
        var value = true;

        // Assert
        value.Should().Be(expected);
    }
}
