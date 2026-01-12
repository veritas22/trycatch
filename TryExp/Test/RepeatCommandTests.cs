using TryExp.Commands;
using TryExp.Core;
using Xunit;

namespace Test;

/// <summary>
/// Тесты для команды RepeatCommand - повторное выполнение команды, выбросившей исключение
/// </summary>
public class RepeatCommandTests
{
    [Fact]
    public void Execute_ShouldExecuteOriginalCommand()
    {
        // Arrange
        var executionCount = 0;
        var originalCommand = new TestCountingCommand(() => executionCount++);
        var repeatCommand = new RepeatCommand(originalCommand);

        // Act
        repeatCommand.Execute();

        // Assert
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public void Execute_ShouldThrowException_WhenOriginalCommandThrows()
    {
        // Arrange
        var originalCommand = new TestFailingCommand("TestCommand");
        var repeatCommand = new RepeatCommand(originalCommand);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => repeatCommand.Execute());
        Assert.Contains("TestCommand", exception.Message);
    }

    [Fact]
    public void Execute_ShouldExecuteOriginalCommandMultipleTimes_WhenCalledMultipleTimes()
    {
        // Arrange
        var executionCount = 0;
        var originalCommand = new TestCountingCommand(() => executionCount++);
        var repeatCommand = new RepeatCommand(originalCommand);

        // Act
        repeatCommand.Execute();
        repeatCommand.Execute();
        repeatCommand.Execute();

        // Assert
        Assert.Equal(3, executionCount);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenOriginalCommandIsNull()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RepeatCommand(null!));
    }

    [Fact]
    public void Unwrap_ShouldReturnOriginalCommand_WhenCommandIsNotWrapped()
    {
        // Arrange
        var originalCommand = new TestSuccessCommand("Test");

        // Act
        var unwrapped = RepeatCommand.Unwrap(originalCommand);

        // Assert
        Assert.Same(originalCommand, unwrapped);
    }

    [Fact]
    public void Unwrap_ShouldReturnOriginalCommand_WhenCommandIsWrappedOnce()
    {
        // Arrange
        var originalCommand = new TestSuccessCommand("Test");
        var wrappedCommand = new RepeatCommand(originalCommand);

        // Act
        var unwrapped = RepeatCommand.Unwrap(wrappedCommand);

        // Assert
        Assert.Same(originalCommand, unwrapped);
    }

    [Fact]
    public void Unwrap_ShouldReturnOriginalCommand_WhenCommandIsWrappedMultipleTimes()
    {
        // Arrange
        var originalCommand = new TestSuccessCommand("Test");
        var wrapped1 = new RepeatCommand(originalCommand);
        var wrapped2 = new RepeatCommand(wrapped1);
        var wrapped3 = new RepeatCommand(wrapped2);

        // Act
        var unwrapped = RepeatCommand.Unwrap(wrapped3);

        // Assert
        Assert.Same(originalCommand, unwrapped);
    }

    [Fact]
    public void Execute_ShouldPreserveOriginalCommandBehavior()
    {
        // Arrange
        var testValue = 0;
        var originalCommand = new TestModifyingCommand(() => testValue = 42);
        var repeatCommand = new RepeatCommand(originalCommand);

        // Act
        repeatCommand.Execute();

        // Assert
        Assert.Equal(42, testValue);
    }
}

/// <summary>
/// Тестовая команда, которая выполняет действие и увеличивает счётчик
/// </summary>
internal class TestCountingCommand : ICommand
{
    private readonly Action _action;

    public TestCountingCommand(Action action)
    {
        _action = action;
    }

    public void Execute()
    {
        _action();
    }
}

/// <summary>
/// Тестовая команда, которая изменяет значение
/// </summary>
internal class TestModifyingCommand : ICommand
{
    private readonly Action _action;

    public TestModifyingCommand(Action action)
    {
        _action = action;
    }

    public void Execute()
    {
        _action();
    }
}
