using TryExp.Strategies;
using TryExp.Commands;
using TryExp.Core;
using Xunit;

namespace Test;

/// <summary>
/// Тесты для стратегии RepeatTwiceThenLogStrategy - повторить команду два раза, потом записать в лог.
/// Используется команда RepeatTwiceCommand, которая показывает, что команду не удалось выполнить два раза.
/// </summary>
public class RepeatTwiceThenLogStrategyTests
{
    [Fact]
    public void RecoverCommand_ShouldCreateRepeatTwiceCommand_OnFirstException()
    {
        // Arrange
        var strategy = new RepeatTwiceThenLogStrategy();
        var failingCommand = new TestFailingCommand("TestCommand");
        var exception = new InvalidOperationException("First exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(failingCommand, exception);

        // Assert
        Assert.NotNull(recoveryCommand);
        Assert.IsType<RepeatTwiceCommand>(recoveryCommand);
        
        var repeatTwiceCommand = Assert.IsType<RepeatTwiceCommand>(recoveryCommand);
        Assert.Equal(2, repeatTwiceCommand.AttemptsLeft);
    }

    [Fact]
    public void RecoverCommand_ShouldContinueRepeating_WhenAttemptsLeft()
    {
        // Arrange
        var strategy = new RepeatTwiceThenLogStrategy();
        var originalCommand = new TestFailingCommand("TestCommand");
        var repeatTwiceCommand = new RepeatTwiceCommand(originalCommand, attempts: 2);
        var exception = new InvalidOperationException("Second exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatTwiceCommand, exception);

        // Assert
        Assert.NotNull(recoveryCommand);
        Assert.IsType<RepeatTwiceCommand>(recoveryCommand);
        
        var returnedCommand = Assert.IsType<RepeatTwiceCommand>(recoveryCommand);
        Assert.True(returnedCommand.AttemptsLeft > 0, "Попытки должны еще оставаться");
    }

    [Fact]
    public void RecoverCommand_ShouldCreateLogCommand_WhenAttemptsExhausted()
    {
        // Arrange
        var strategy = new RepeatTwiceThenLogStrategy();
        var originalCommand = new TestFailingCommand("TestCommand");
        var repeatTwiceCommand = new RepeatTwiceCommand(originalCommand, attempts: 1);
        
        // Симулируем выполнение, которое уменьшает количество попыток
        try
        {
            repeatTwiceCommand.Execute();
        }
        catch { }
        
        // Теперь попытки закончились (AttemptsLeft <= 0)
        var exception = new InvalidOperationException("Third exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatTwiceCommand, exception);

        // Assert
        Assert.NotNull(recoveryCommand);
        Assert.IsType<LogCommand>(recoveryCommand);
    }

    [Fact]
    public void FullFlow_FirstException_ShouldCreateRepeatTwiceCommandWithTwoAttempts()
    {
        // Arrange
        var strategy = new RepeatTwiceThenLogStrategy();
        var failingCommand = new TestFailingCommand("TestCommand");
        var exception = new InvalidOperationException("First exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(failingCommand, exception);

        // Assert
        var repeatTwiceCommand = Assert.IsType<RepeatTwiceCommand>(recoveryCommand);
        Assert.Equal(2, repeatTwiceCommand.AttemptsLeft);
        
        // Проверяем, что команда содержит оригинальную команду
        var unwrapped = RepeatCommand.Unwrap(repeatTwiceCommand);
        Assert.Same(failingCommand, unwrapped);
    }

    [Fact]
    public void FullFlow_SecondException_ShouldContinueRepeating()
    {
        // Arrange
        var strategy = new RepeatTwiceThenLogStrategy();
        var originalCommand = new TestFailingCommand("TestCommand");
        var repeatTwiceCommand = new RepeatTwiceCommand(originalCommand, attempts: 2);
        var exception = new InvalidOperationException("Second exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatTwiceCommand, exception);

        // Assert
        var returnedCommand = Assert.IsType<RepeatTwiceCommand>(recoveryCommand);
        Assert.Equal(2, returnedCommand.AttemptsLeft);
    }

    [Fact]
    public void FullFlow_ThirdException_ShouldCreateLogCommand()
    {
        // Arrange
        var strategy = new RepeatTwiceThenLogStrategy();
        var testLogPath = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");
        var originalCommand = new TestFailingCommand("TestCommand");
        
        // Создаем RepeatTwiceCommand и симулируем две неудачные попытки
        var repeatTwiceCommand = new RepeatTwiceCommand(originalCommand, attempts: 2);
        
        // Первая попытка
        try { repeatTwiceCommand.Execute(); } catch { }
        // Вторая попытка
        try { repeatTwiceCommand.Execute(); } catch { }
        
        // Теперь попытки закончились
        var exception = new InvalidOperationException("Third exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatTwiceCommand, exception);
        var logCommand = Assert.IsType<LogCommand>(recoveryCommand);
        
        // Создаём LogCommand с тестовым путём и выполняем
        var logCommandWithPath = new LogCommand(originalCommand, exception, testLogPath);
        logCommandWithPath.Execute();

        // Assert
        Assert.True(File.Exists(testLogPath));
        var logContent = File.ReadAllText(testLogPath);
        Assert.Contains("TestCommand", logContent);
        Assert.Contains("InvalidOperationException", logContent);
        Assert.Contains("Third exception", logContent);
        
        // Cleanup
        File.Delete(testLogPath);
    }

    [Fact]
    public void RecoverCommand_ShouldUnwrapOriginalCommand_WhenCreatingLogCommand()
    {
        // Arrange
        var strategy = new RepeatTwiceThenLogStrategy();
        var originalCommand = new TestFailingCommand("OriginalCommand");
        var repeatTwiceCommand = new RepeatTwiceCommand(originalCommand, attempts: 1);
        
        // Симулируем выполнение
        try { repeatTwiceCommand.Execute(); } catch { }
        
        var exception = new InvalidOperationException("Exception");
        var testLogPath = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatTwiceCommand, exception);
        var logCommand = Assert.IsType<LogCommand>(recoveryCommand);
        
        var logCommandWithPath = new LogCommand(originalCommand, exception, testLogPath);
        logCommandWithPath.Execute();

        // Assert
        var logContent = File.ReadAllText(testLogPath);
        // Проверяем, что в логе указана оригинальная команда, а не RepeatTwiceCommand
        Assert.Contains("OriginalCommand", logContent);
        Assert.DoesNotContain("RepeatTwiceCommand", logContent);
        
        // Cleanup
        File.Delete(testLogPath);
    }

    [Fact]
    public void RepeatTwiceCommand_ShouldDecreaseAttemptsLeft_OnEachExecution()
    {
        // Arrange
        var originalCommand = new TestFailingCommand("TestCommand");
        var repeatTwiceCommand = new RepeatTwiceCommand(originalCommand, attempts: 2);

        // Assert - начальное состояние
        Assert.Equal(2, repeatTwiceCommand.AttemptsLeft);

        // Act & Assert - первая попытка
        try { repeatTwiceCommand.Execute(); } catch { }
        Assert.Equal(1, repeatTwiceCommand.AttemptsLeft);

        // Act & Assert - вторая попытка
        try { repeatTwiceCommand.Execute(); } catch { }
        Assert.Equal(0, repeatTwiceCommand.AttemptsLeft);
    }

    [Fact]
    public void Strategy_ShouldHandleDifferentExceptionTypes()
    {
        // Arrange
        var strategy = new RepeatTwiceThenLogStrategy();
        var failingCommand = new TestFailingCommand("TestCommand");

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
            // Первое исключение - должен вернуть RepeatTwiceCommand
            var recoveryCommand1 = strategy.RecoverCommand(failingCommand, exception);
            Assert.IsType<RepeatTwiceCommand>(recoveryCommand1);
        }
    }

    [Fact]
    public void FullFlow_CompleteScenario_FirstSecondThirdException()
    {
        // Arrange
        var strategy = new RepeatTwiceThenLogStrategy();
        var testLogPath = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");
        var originalCommand = new TestFailingCommand("TestCommand");
        var firstException = new InvalidOperationException("First exception");

        // Act - первое исключение
        var recoveryCommand1 = strategy.RecoverCommand(originalCommand, firstException);
        var repeatTwiceCommand1 = Assert.IsType<RepeatTwiceCommand>(recoveryCommand1);
        Assert.Equal(2, repeatTwiceCommand1.AttemptsLeft);

        // Второе исключение (после первой попытки)
        var secondException = new InvalidOperationException("Second exception");
        try { repeatTwiceCommand1.Execute(); } catch { }
        
        var recoveryCommand2 = strategy.RecoverCommand(repeatTwiceCommand1, secondException);
        var repeatTwiceCommand2 = Assert.IsType<RepeatTwiceCommand>(recoveryCommand2);
        Assert.True(repeatTwiceCommand2.AttemptsLeft > 0);

        // Третье исключение (после второй попытки)
        var thirdException = new InvalidOperationException("Third exception");
        try { repeatTwiceCommand2.Execute(); } catch { }
        
        var recoveryCommand3 = strategy.RecoverCommand(repeatTwiceCommand2, thirdException);
        var logCommand = Assert.IsType<LogCommand>(recoveryCommand3);

        // Выполняем LogCommand
        var logCommandWithPath = new LogCommand(originalCommand, thirdException, testLogPath);
        logCommandWithPath.Execute();

        // Assert
        Assert.True(File.Exists(testLogPath));
        var logContent = File.ReadAllText(testLogPath);
        Assert.Contains("TestCommand", logContent);
        Assert.Contains("Third exception", logContent);
        
        // Cleanup
        File.Delete(testLogPath);
    }
}
