using TryExp.Core;
using TryExp.Commands;
using TryExp.Strategies;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Test;

/// <summary>
/// Интеграционные тесты для стратегии RepeatTwiceThenLogStrategy с CommandProcessor.
/// Проверяет работу стратегии "повторить два раза, потом записать в лог" в реальной очереди команд.
/// </summary>
public class RepeatTwiceThenLogIntegrationTests
{
    [Fact]
    public async Task CommandProcessor_ShouldHandleRepeatTwiceThenLog_Scenario()
    {
        // Arrange
        var queue = new BlockingCollection<ICommand>(100);
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
        var strategies = new IExceptionStrategy[] 
        { 
            new LogStrategy(),
            new RepeatTwiceThenLogStrategy() 
        };
        
        var registry = new ExceptionStrategyRegistry(strategies, 
            loggerFactory.CreateLogger<ExceptionStrategyRegistry>());
        
        var processor = new CommandProcessor(queue, registry, 
            loggerFactory.CreateLogger<CommandProcessor>());

        var executionCount = 0;
        var attemptCount = 0;
        
        // Команда, которая выбрасывает исключение первые три раза
        var command = new TestFailingCommandWithAttempts("TestCommand", 
            () => executionCount++, 
            () => attemptCount++ < 3);
        
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
        // Должна быть обработана RepeatTwiceCommand (при первом исключении)
        Assert.Contains(typeof(RepeatTwiceCommand), processedCommands);
        
        // Cleanup
        if (File.Exists(logPath))
        {
            File.Delete(logPath);
        }
    }

    [Fact]
    public void RepeatTwiceCommand_ShouldTrackAttemptsCorrectly()
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
    public void Strategy_ShouldCreateLogCommand_AfterTwoFailedAttempts()
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
        var exception = new InvalidOperationException("Exception after two attempts");

        // Act
        var recoveryCommand = strategy.RecoverCommand(repeatTwiceCommand, exception);
        var logCommand = Assert.IsType<LogCommand>(recoveryCommand);
        
        var logCommandWithPath = new LogCommand(originalCommand, exception, testLogPath);
        logCommandWithPath.Execute();

        // Assert
        Assert.True(File.Exists(testLogPath));
        var logContent = File.ReadAllText(testLogPath);
        Assert.Contains("TestCommand", logContent);
        Assert.Contains("Exception after two attempts", logContent);
        
        // Cleanup
        File.Delete(testLogPath);
    }
}
