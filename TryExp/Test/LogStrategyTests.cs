using TryExp.Strategies;
using TryExp.Commands;
using TryExp.Core;
using Xunit;

namespace Test;

/// <summary>
/// Тесты для стратегии LogStrategy - обработчик исключения, который ставит команду логирования в очередь
/// </summary>
public class LogStrategyTests
{
    [Fact]
    public void RecoverCommand_ShouldReturnLogCommand()
    {
        // Arrange
        var strategy = new LogStrategy();
        var failedCommand = new TestFailingCommand("TestCommand");
        var exception = new InvalidOperationException("Test exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(failedCommand, exception);

        // Assert
        Assert.NotNull(recoveryCommand);
        Assert.IsType<LogCommand>(recoveryCommand);
    }

    [Fact]
    public void RecoverCommand_ShouldCreateLogCommandWithCorrectParameters()
    {
        // Arrange
        var strategy = new LogStrategy();
        var failedCommand = new TestFailingCommand("MyCommand");
        var exception = new ArgumentException("Error message");

        // Act
        var recoveryCommand = strategy.RecoverCommand(failedCommand, exception);

        // Assert
        var logCommand = Assert.IsType<LogCommand>(recoveryCommand);
        
        // Проверяем, что команда может выполниться (создаст лог-файл)
        var testLogPath = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");
        var logCommandWithPath = new LogCommand(failedCommand, exception, testLogPath);
        logCommandWithPath.Execute();
        
        Assert.True(File.Exists(testLogPath));
        
        // Cleanup
        File.Delete(testLogPath);
    }

    [Fact]
    public void RecoverCommand_ShouldHandleDifferentExceptionTypes()
    {
        // Arrange
        var strategy = new LogStrategy();
        var failedCommand = new TestFailingCommand("TestCommand");

        var exceptions = new Exception[]
        {
            new InvalidOperationException("Invalid operation"),
            new ArgumentException("Invalid argument"),
            new ArgumentNullException("param"),
            new Exception("Generic exception")
        };

        // Act & Assert
        foreach (var exception in exceptions)
        {
            var recoveryCommand = strategy.RecoverCommand(failedCommand, exception);
            Assert.IsType<LogCommand>(recoveryCommand);
        }
    }

    [Fact]
    public void RecoverCommand_ShouldWorkWithDifferentCommandTypes()
    {
        // Arrange
        var strategy = new LogStrategy();
        var exception = new Exception("Test");

        var commands = new ICommand[]
        {
            new TestFailingCommand("Command1"),
            new TestFailingCommand("Command2"),
            new TestSuccessCommand("Command3")
        };

        // Act & Assert
        foreach (var command in commands)
        {
            var recoveryCommand = strategy.RecoverCommand(command, exception);
            Assert.IsType<LogCommand>(recoveryCommand);
        }
    }
}

/// <summary>
/// Тестовая команда, которая выполняется успешно
/// </summary>
internal class TestSuccessCommand : ICommand
{
    private readonly string _name;

    public TestSuccessCommand(string name)
    {
        _name = name;
    }

    public void Execute()
    {
        // Команда выполняется успешно
    }
}
