using TryExp.Strategies;
using TryExp.Commands;
using TryExp.Core;
using Xunit;

namespace Test;

/// <summary>
/// Тесты для стратегии RepeatOnceThenLogStrategy - обработчик исключения,
/// который ставит команду-повторитель в очередь, а при повторном исключении - команду логирования
/// </summary>
public class RepeatOnceThenLogStrategyTests
{
    [Fact]
    public void RecoverCommand_ShouldReturnRepeatCommand_WhenFirstException()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var failedCommand = new TestFailingCommand("TestCommand");
        var exception = new InvalidOperationException("Test exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(failedCommand, exception);

        // Assert
        Assert.NotNull(recoveryCommand);
        Assert.IsType<RepeatCommand>(recoveryCommand);
    }

    [Fact]
    public void RecoverCommand_ShouldReturnLogCommand_WhenRepeatCommandFails()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var originalCommand = new TestFailingCommand("TestCommand");
        var repeatCommand = new RepeatCommand(originalCommand);
        var exception = new InvalidOperationException("Test exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatCommand, exception);

        // Assert
        Assert.NotNull(recoveryCommand);
        Assert.IsType<LogCommand>(recoveryCommand);
    }

    [Fact]
    public void RecoverCommand_ShouldUnwrapOriginalCommand_WhenCreatingLogCommand()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var originalCommand = new TestFailingCommand("OriginalCommand");
        var repeatCommand = new RepeatCommand(originalCommand);
        var exception = new InvalidOperationException("Test exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatCommand, exception);

        // Assert
        var logCommand = Assert.IsType<LogCommand>(recoveryCommand);
        
        // Проверяем, что LogCommand содержит оригинальную команду
        var testLogPath = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");
        var logCommandWithPath = new LogCommand(originalCommand, exception, testLogPath);
        logCommandWithPath.Execute();
        
        var logContent = File.ReadAllText(testLogPath);
        Assert.Contains("OriginalCommand", logContent);
        
        // Cleanup
        File.Delete(testLogPath);
    }

    [Fact]
    public void RecoverCommand_ShouldHandleNestedRepeatCommands()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var originalCommand = new TestFailingCommand("TestCommand");
        var repeat1 = new RepeatCommand(originalCommand);
        var repeat2 = new RepeatCommand(repeat1);
        var exception = new Exception("Test");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeat2, exception);

        // Assert
        Assert.IsType<LogCommand>(recoveryCommand);
    }

    [Fact]
    public void RecoverCommand_ShouldWorkWithDifferentExceptionTypes()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var failedCommand = new TestFailingCommand("TestCommand");

        var exceptions = new Exception[]
        {
            new InvalidOperationException("Invalid operation"),
            new ArgumentException("Invalid argument"),
            new Exception("Generic exception")
        };

        // Act & Assert
        foreach (var exception in exceptions)
        {
            // Первое исключение - должен вернуть RepeatCommand
            var recoveryCommand1 = strategy.RecoverCommand(failedCommand, exception);
            Assert.IsType<RepeatCommand>(recoveryCommand1);

            // Повторное исключение - должен вернуть LogCommand
            var recoveryCommand2 = strategy.RecoverCommand(recoveryCommand1, exception);
            Assert.IsType<LogCommand>(recoveryCommand2);
        }
    }

    [Fact]
    public void RecoverCommand_ShouldCreateRepeatCommandThatCanBeExecuted()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var executionCount = 0;
        var originalCommand = new TestCountingCommand(() => executionCount++);
        var exception = new Exception("Test");

        // Act
        var recoveryCommand = strategy.RecoverCommand(originalCommand, exception);
        var repeatCommand = Assert.IsType<RepeatCommand>(recoveryCommand);
        
        // Выполняем RepeatCommand - он должен выполнить оригинальную команду
        repeatCommand.Execute();

        // Assert
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public void RecoverCommand_ShouldCreateLogCommandThatWritesToFile()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var originalCommand = new TestFailingCommand("TestCommand");
        var repeatCommand = new RepeatCommand(originalCommand);
        var exception = new InvalidOperationException("Test exception");
        var testLogPath = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatCommand, exception);
        var logCommand = Assert.IsType<LogCommand>(recoveryCommand);
        
        // Создаём LogCommand с тестовым путём и выполняем
        var logCommandWithPath = new LogCommand(originalCommand, exception, testLogPath);
        logCommandWithPath.Execute();

        // Assert
        Assert.True(File.Exists(testLogPath));
        var logContent = File.ReadAllText(testLogPath);
        Assert.Contains("TestCommand", logContent);
        Assert.Contains("InvalidOperationException", logContent);
        Assert.Contains("Test exception", logContent);
        
        // Cleanup
        File.Delete(testLogPath);
    }
}
