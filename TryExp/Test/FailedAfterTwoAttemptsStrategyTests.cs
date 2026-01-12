using TryExp.Strategies;
using TryExp.Commands;
using TryExp.Core;
using Xunit;

namespace Test;

/// <summary>
/// Тесты для стратегии FailedAfterTwoAttemptsStrategy - повторить команду два раза,
/// затем создать FailedAfterTwoAttemptsCommand для маркировки неудачи.
/// </summary>
public class FailedAfterTwoAttemptsStrategyTests
{
    [Fact]
    public void RecoverCommand_ShouldCreateRepeatTwiceCommand_OnFirstException()
    {
        // Arrange
        var strategy = new FailedAfterTwoAttemptsStrategy();
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
        var strategy = new FailedAfterTwoAttemptsStrategy();
        var originalCommand = new TestFailingCommand("TestCommand");
        var repeatTwiceCommand = new RepeatTwiceCommand(originalCommand, attempts: 2);
        var exception = new InvalidOperationException("Second exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatTwiceCommand, exception);

        // Assert
        Assert.NotNull(recoveryCommand);
        Assert.IsType<RepeatTwiceCommand>(recoveryCommand);
        
        var returnedCommand = Assert.IsType<RepeatTwiceCommand>(recoveryCommand);
        Assert.True(returnedCommand.AttemptsLeft > 0);
    }

    [Fact]
    public void RecoverCommand_ShouldCreateFailedAfterTwoAttemptsCommand_WhenAttemptsExhausted()
    {
        // Arrange
        var strategy = new FailedAfterTwoAttemptsStrategy();
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
        Assert.IsType<FailedAfterTwoAttemptsCommand>(recoveryCommand);
    }

    [Fact]
    public void FailedAfterTwoAttemptsCommand_ShouldThrowException_WhenExecuted()
    {
        // Arrange
        var strategy = new FailedAfterTwoAttemptsStrategy();
        var originalCommand = new TestFailingCommand("TestCommand");
        var repeatTwiceCommand = new RepeatTwiceCommand(originalCommand, attempts: 1);
        
        try { repeatTwiceCommand.Execute(); } catch { }
        
        var exception = new InvalidOperationException("Exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatTwiceCommand, exception);
        var failedCommand = Assert.IsType<FailedAfterTwoAttemptsCommand>(recoveryCommand);

        // Assert
        var thrownException = Assert.Throws<InvalidOperationException>(() => failedCommand.Execute());
        Assert.Contains("TestCommand", thrownException.Message);
        Assert.Contains("не смогла выполниться после двух попыток", thrownException.Message);
    }

    [Fact]
    public void FullFlow_FirstException_ShouldCreateRepeatTwiceCommand()
    {
        // Arrange
        var strategy = new FailedAfterTwoAttemptsStrategy();
        var failingCommand = new TestFailingCommand("TestCommand");
        var exception = new InvalidOperationException("First exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(failingCommand, exception);

        // Assert
        var repeatTwiceCommand = Assert.IsType<RepeatTwiceCommand>(recoveryCommand);
        Assert.Equal(2, repeatTwiceCommand.AttemptsLeft);
        
        var unwrapped = RepeatCommand.Unwrap(repeatTwiceCommand);
        Assert.Same(failingCommand, unwrapped);
    }

    [Fact]
    public void FullFlow_SecondException_ShouldContinueRepeating()
    {
        // Arrange
        var strategy = new FailedAfterTwoAttemptsStrategy();
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
    public void FullFlow_ThirdException_ShouldCreateFailedAfterTwoAttemptsCommand()
    {
        // Arrange
        var strategy = new FailedAfterTwoAttemptsStrategy();
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

        // Assert
        var failedCommand = Assert.IsType<FailedAfterTwoAttemptsCommand>(recoveryCommand);
        
        // Проверяем, что команда выбрасывает исключение
        var thrownException = Assert.Throws<InvalidOperationException>(() => failedCommand.Execute());
        Assert.Contains("TestCommand", thrownException.Message);
    }

    [Fact]
    public void RecoverCommand_ShouldUnwrapOriginalCommand_WhenCreatingFailedCommand()
    {
        // Arrange
        var strategy = new FailedAfterTwoAttemptsStrategy();
        var originalCommand = new TestFailingCommand("OriginalCommand");
        var repeatTwiceCommand = new RepeatTwiceCommand(originalCommand, attempts: 1);
        
        try { repeatTwiceCommand.Execute(); } catch { }
        
        var exception = new InvalidOperationException("Exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatTwiceCommand, exception);
        var failedCommand = Assert.IsType<FailedAfterTwoAttemptsCommand>(recoveryCommand);

        // Assert
        var thrownException = Assert.Throws<InvalidOperationException>(() => failedCommand.Execute());
        // Проверяем, что в сообщении указана оригинальная команда, а не RepeatTwiceCommand
        Assert.Contains("OriginalCommand", thrownException.Message);
        Assert.DoesNotContain("RepeatTwiceCommand", thrownException.Message);
    }

    [Fact]
    public void FullFlow_CompleteScenario_FirstSecondThirdException()
    {
        // Arrange
        var strategy = new FailedAfterTwoAttemptsStrategy();
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
        var failedCommand = Assert.IsType<FailedAfterTwoAttemptsCommand>(recoveryCommand3);

        // Assert
        var thrownException = Assert.Throws<InvalidOperationException>(() => failedCommand.Execute());
        Assert.Contains("TestCommand", thrownException.Message);
        Assert.Contains("не смогла выполниться после двух попыток", thrownException.Message);
    }

    [Fact]
    public void Strategy_ShouldHandleDifferentExceptionTypes()
    {
        // Arrange
        var strategy = new FailedAfterTwoAttemptsStrategy();
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
            var recoveryCommand = strategy.RecoverCommand(failingCommand, exception);
            Assert.IsType<RepeatTwiceCommand>(recoveryCommand);
        }
    }
}
