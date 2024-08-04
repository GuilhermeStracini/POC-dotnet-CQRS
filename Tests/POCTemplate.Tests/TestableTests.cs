using FluentAssertions;

namespace POCTemplate.Tests;

public class TestableTests
{
    [Fact]
    public void TestableMethod_StateUnderTest_ExpectedBehavior()
    {
        // Arrange
        var testable = new Testable();

        // Act
        var result = testable.TestableMethod();

        // Assert
        result.Should().BeTrue();
    }
}
