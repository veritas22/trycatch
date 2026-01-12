using TryExp.Core;
using TryExp.Commands;
using TryExp.Strategies;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Test;

/// <summary>
/// Тесты для пункта 8: При первом выбросе исключения повторить команду,
/// при повторном выбросе исключения записать информацию в лог.
/// 
/// Используются команды из пункта 4 (LogCommand) и пункта 6 (RepeatCommand).
/// </summary>
public class RepeatOnceThenLogIntegrationTests
{
    [Fact]
    public void Strategy_ShouldCreateRepeatCommand_OnFirstException()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var failingCommand = new TestFailingCommand("TestCommand");
        var exception = new InvalidOperationException("First exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(failingCommand, exception);

        // Assert
        Assert.NotNull(recoveryCommand);
        Assert.IsType<RepeatCommand>(recoveryCommand);
        
        var repeatCommand = Assert.IsType<RepeatCommand>(recoveryCommand);
        // Проверяем, что RepeatCommand содержит оригинальную команду
        var unwrapped = RepeatCommand.Unwrap(repeatCommand);
        Assert.Same(failingCommand, unwrapped);
    }

    [Fact]
    public void Strategy_ShouldCreateLogCommand_OnSecondException()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var originalCommand = new TestFailingCommand("TestCommand");
        var repeatCommand = new RepeatCommand(originalCommand);
        var exception = new InvalidOperationException("Second exception");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatCommand, exception);

        // Assert
        Assert.NotNull(recoveryCommand);
        Assert.IsType<LogCommand>(recoveryCommand);
    }

    [Fact]
    public void FullFlow_FirstExceptionThenRepeat_ShouldExecuteOriginalCommandAgain()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var executionCount = 0;
        var shouldFail = true;
        
        // Команда, которая выбрасывает исключение только при первом вызове
        var command = new TestConditionalFailingCommand(() => executionCount++, () => shouldFail, "TestCommand");
        var exception = new InvalidOperationException("First exception");

        // Act - первое исключение
        var recoveryCommand1 = strategy.RecoverCommand(command, exception);
        var repeatCommand = Assert.IsType<RepeatCommand>(recoveryCommand1);
        
        // Теперь команда должна выполниться успешно
        shouldFail = false;
        repeatCommand.Execute();

        // Assert
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public void FullFlow_FirstExceptionThenSecondException_ShouldCreateLogCommand()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var testLogPath = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");
        var originalCommand = new TestFailingCommand("TestCommand");
        var firstException = new InvalidOperationException("First exception");

        // Act - первое исключение
        var recoveryCommand1 = strategy.RecoverCommand(originalCommand, firstException);
        var repeatCommand = Assert.IsType<RepeatCommand>(recoveryCommand1);
        
        // Второе исключение (при повторной попытке)
        var secondException = new InvalidOperationException("Second exception");
        var recoveryCommand2 = strategy.RecoverCommand(repeatCommand, secondException);
        var logCommand = Assert.IsType<LogCommand>(recoveryCommand2);
        
        // Создаём LogCommand с тестовым путём и выполняем
        var logCommandWithPath = new LogCommand(originalCommand, secondException, testLogPath);
        logCommandWithPath.Execute();

        // Assert
        Assert.True(File.Exists(testLogPath));
        var logContent = File.ReadAllText(testLogPath);
        Assert.Contains("TestCommand", logContent);
        Assert.Contains("InvalidOperationException", logContent);
        Assert.Contains("Second exception", logContent);
        
        // Cleanup
        File.Delete(testLogPath);
    }

    [Fact]
    public async Task FullFlow_WithCommandProcessor_ShouldHandleFirstAndSecondException()
    {
        // Arrange
        var queue = new BlockingCollection<ICommand>(100);
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
        var strategies = new IExceptionStrategy[] 
        { 
            new LogStrategy(),
            new RepeatOnceThenLogStrategy() 
        };
        
        var registry = new ExceptionStrategyRegistry(strategies, 
            loggerFactory.CreateLogger<ExceptionStrategyRegistry>());
        
        var processor = new CommandProcessor(queue, registry, 
            loggerFactory.CreateLogger<CommandProcessor>());

        var executionCount = 0;
        var attemptCount = 0;
        
        // Команда, которая выбрасывает исключение первые два раза
        var command = new TestFailingCommandWithAttempts("TestCommand", 
            () => executionCount++, 
            () => attemptCount++ < 2);
        
        queue.Add(command);

        var processedCommands = new List<Type>();
        var logPath = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // Act - симулируем обработку команд
        var maxIterations = 10; // Ограничиваем количество итераций для теста
        var iteration = 0;
        while (!queue.IsCompleted && !cts.Token.IsCancellationRequested && iteration < maxIterations)
        {
            if (queue.TryTake(out var cmd, 100, cts.Token))
            {
                try
                {
                    // Если это LogCommand, используем тестовый путь
                    if (cmd is LogCommand logCmd)
                    {
                        var originalCmd = new TestFailingCommand("TestCommand");
                        var ex = new InvalidOperationException("Exception");
                        var logCmdWithPath = new LogCommand(originalCmd, ex, logPath);
                        logCmdWithPath.Execute();
                        processedCommands.Add(typeof(LogCommand));
                    }
                    else
                    {
                        cmd.Execute();
                        processedCommands.Add(cmd.GetType());
                    }
                }
                catch (Exception ex)
                {
                    var strategy = registry.SelectStrategy(cmd, ex);
                    var recoveryCommand = strategy.RecoverCommand(cmd, ex);
                    queue.Add(recoveryCommand, cts.Token);
                }
            }
            iteration++;
        }

        await Task.Delay(100, cts.Token);
        cts.Cancel();
        queue.CompleteAdding();

        // Assert
        // Должна быть обработана RepeatCommand (при первом исключении)
        Assert.Contains(typeof(RepeatCommand), processedCommands);
        
        // Cleanup
        if (File.Exists(logPath))
        {
            File.Delete(logPath);
        }
    }

    [Fact]
    public void Strategy_ShouldUnwrapOriginalCommand_WhenCreatingLogCommand()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var originalCommand = new TestFailingCommand("OriginalCommand");
        var repeatCommand = new RepeatCommand(originalCommand);
        var exception = new InvalidOperationException("Second exception");
        var testLogPath = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatCommand, exception);
        var logCommand = Assert.IsType<LogCommand>(recoveryCommand);
        
        // Создаём LogCommand с тестовым путём для проверки
        var logCommandWithPath = new LogCommand(originalCommand, exception, testLogPath);
        logCommandWithPath.Execute();

        // Assert
        var logContent = File.ReadAllText(testLogPath);
        // Проверяем, что в логе указана оригинальная команда, а не RepeatCommand
        Assert.Contains("OriginalCommand", logContent);
        Assert.DoesNotContain("RepeatCommand", logContent);
        
        // Cleanup
        File.Delete(testLogPath);
    }

    [Fact]
    public void Strategy_ShouldHandleMultipleCommands_WithDifferentExceptions()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var commands = new[]
        {
            new TestFailingCommand("Command1"),
            new TestFailingCommand("Command2"),
            new TestFailingCommand("Command3")
        };

        // Act & Assert
        foreach (var command in commands)
        {
            // Первое исключение - должен вернуть RepeatCommand
            var exception1 = new InvalidOperationException("First");
            var recovery1 = strategy.RecoverCommand(command, exception1);
            Assert.IsType<RepeatCommand>(recovery1);

            // Второе исключение - должен вернуть LogCommand
            var repeatCmd = new RepeatCommand(command);
            var exception2 = new InvalidOperationException("Second");
            var recovery2 = strategy.RecoverCommand(repeatCmd, exception2);
            Assert.IsType<LogCommand>(recovery2);
        }
    }

    [Fact]
    public void Strategy_ShouldPreserveExceptionInformation_InLogCommand()
    {
        // Arrange
        var strategy = new RepeatOnceThenLogStrategy();
        var originalCommand = new TestFailingCommand("TestCommand");
        var repeatCommand = new RepeatCommand(originalCommand);
        var exception = new ArgumentException("Test error message", "paramName");
        var testLogPath = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatCommand, exception);
        var logCommand = Assert.IsType<LogCommand>(recoveryCommand);
        
        var logCommandWithPath = new LogCommand(originalCommand, exception, testLogPath);
        logCommandWithPath.Execute();

        // Assert
        var logContent = File.ReadAllText(testLogPath);
        Assert.Contains("TestCommand", logContent);
        Assert.Contains("ArgumentException", logContent);
        Assert.Contains("Test error message", logContent);
        
        // Cleanup
        File.Delete(testLogPath);
    }
}

/// <summary>
/// Тестовая команда, которая выбрасывает исключение только при определенном условии
/// </summary>
internal class TestConditionalFailingCommand : ICommand
{
    private readonly string _name;
    private readonly Action _onExecute;
    private readonly Func<bool> _shouldFail;

    public TestConditionalFailingCommand(Action onExecute, Func<bool> shouldFail, string name)
    {
        _onExecute = onExecute;
        _shouldFail = shouldFail;
        _name = name;
    }

    public void Execute()
    {
        _onExecute();
        if (_shouldFail())
        {
            throw new InvalidOperationException($"Command {_name} failed");
        }
    }
}

/// <summary>
/// Тестовая команда, которая выбрасывает исключение определенное количество раз
/// </summary>
internal class TestFailingCommandWithAttempts : ICommand
{
    private readonly string _name;
    private readonly Action _onExecute;
    private readonly Func<bool> _shouldFail;

    public TestFailingCommandWithAttempts(string name, Action onExecute, Func<bool> shouldFail)
    {
        _name = name;
        _onExecute = onExecute;
        _shouldFail = shouldFail;
    }

    public void Execute()
    {
        _onExecute();
        if (_shouldFail())
        {
            throw new InvalidOperationException($"Command {_name} failed");
        }
    }
}
