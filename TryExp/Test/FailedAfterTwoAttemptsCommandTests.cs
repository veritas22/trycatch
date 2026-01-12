using TryExp.Commands;
using TryExp.Core;
using Xunit;

namespace Test;

/// <summary>
/// Тесты для команды FailedAfterTwoAttemptsCommand - команда, которая показывает,
/// что команду не удалось выполнить два раза (как указано в задании).
/// </summary>
public class FailedAfterTwoAttemptsCommandTests
{
    [Fact]
    public void Execute_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var originalCommand = new TestFailingCommand("TestCommand");
        var failedCommand = new FailedAfterTwoAttemptsCommand(originalCommand);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => failedCommand.Execute());
        Assert.Contains("TestCommand", exception.Message);
        Assert.Contains("не смогла выполниться после двух попыток", exception.Message);
    }

    [Fact]
    public void Execute_ShouldContainOriginalCommandName_InExceptionMessage()
    {
        // Arrange
        var originalCommand = new TestFailingCommand("MyTestCommand");
        var failedCommand = new FailedAfterTwoAttemptsCommand(originalCommand);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => failedCommand.Execute());
        Assert.Contains("MyTestCommand", exception.Message);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenOriginalCommandIsNull()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FailedAfterTwoAttemptsCommand(null!));
    }

    [Fact]
    public void Execute_ShouldAlwaysThrowException_RegardlessOfOriginalCommand()
    {
        // Arrange
        var successCommand = new TestSuccessCommand("SuccessCommand");
        var failedCommand = new FailedAfterTwoAttemptsCommand(successCommand);

        // Act & Assert
        // Даже если оригинальная команда успешна, FailedAfterTwoAttemptsCommand должна выбрасывать исключение
        Assert.Throws<InvalidOperationException>(() => failedCommand.Execute());
    }

    [Fact]
    public void Execute_ShouldThrowException_WithCorrectMessageFormat()
    {
        // Arrange
        var originalCommand = new TestFailingCommand("CommandName");
        var failedCommand = new FailedAfterTwoAttemptsCommand(originalCommand);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => failedCommand.Execute());

        // Assert
        Assert.NotNull(exception.Message);
        Assert.Contains("CommandName", exception.Message);
        Assert.Contains("не смогла выполниться", exception.Message);
        Assert.Contains("двух попыток", exception.Message);
    }
}
