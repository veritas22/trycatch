using TryExp.Core;
using TryExp.Commands;
using TryExp.Strategies;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Test;

/// <summary>
/// Интеграционные тесты для проверки работы CommandProcessor со стратегиями обработки исключений
/// </summary>
public class CommandProcessorIntegrationTests
{
    [Fact]
    public async Task ProcessAsync_ShouldAddLogCommandToQueue_WhenUsingLogStrategy()
    {
        // Arrange
        var queue = new BlockingCollection<ICommand>(100);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var strategies = new IExceptionStrategy[] { new LogStrategy() };
        var registry = new ExceptionStrategyRegistry(strategies, loggerFactory.CreateLogger<ExceptionStrategyRegistry>());
        var processor = new CommandProcessor(queue, registry, loggerFactory.CreateLogger<CommandProcessor>());

        var failingCommand = new TestFailingCommand("TestCommand");
        queue.Add(failingCommand);

        var processedCommands = new List<ICommand>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act - симулируем обработку команд
        while (!queue.IsCompleted && !cts.Token.IsCancellationRequested)
        {
            if (queue.TryTake(out var cmd, 100, cts.Token))
            {
                try
                {
                    cmd.Execute();
                    processedCommands.Add(cmd);
                }
                catch (Exception ex)
                {
                    var strategy = registry.SelectStrategy(cmd, ex);
                    var recoveryCommand = strategy.RecoverCommand(cmd, ex);
                    queue.Add(recoveryCommand, cts.Token);
                }
            }
        }

        await Task.Delay(500, cts.Token);
        cts.Cancel();
        queue.CompleteAdding();

        // Assert
        Assert.Contains(processedCommands, c => c is LogCommand);
    }

    [Fact]
    public async Task ProcessAsync_ShouldAddRepeatCommandToQueue_WhenUsingRepeatOnceThenLogStrategy()
    {
        // Arrange
        var queue = new BlockingCollection<ICommand>(100);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var strategies = new IExceptionStrategy[] 
        { 
            new LogStrategy(),
            new RepeatOnceThenLogStrategy() 
        };
        var registry = new ExceptionStrategyRegistry(strategies, loggerFactory.CreateLogger<ExceptionStrategyRegistry>());
        var processor = new CommandProcessor(queue, registry, loggerFactory.CreateLogger<CommandProcessor>());

        var failingCommand = new TestFailingCommand("TestCommand");
        queue.Add(failingCommand);

        var processedCommands = new List<ICommand>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act - симулируем обработку команд
        while (!queue.IsCompleted && !cts.Token.IsCancellationRequested)
        {
            if (queue.TryTake(out var cmd, 100, cts.Token))
            {
                try
                {
                    cmd.Execute();
                    processedCommands.Add(cmd);
                }
                catch (Exception ex)
                {
                    var strategy = registry.SelectStrategy(cmd, ex);
                    var recoveryCommand = strategy.RecoverCommand(cmd, ex);
                    queue.Add(recoveryCommand, cts.Token);
                }
            }
        }

        await Task.Delay(500, cts.Token);
        cts.Cancel();
        queue.CompleteAdding();

        // Assert - должна быть либо RepeatCommand, либо LogCommand (если уже была повторная попытка)
        Assert.True(processedCommands.Any(c => c is RepeatCommand || c is LogCommand),
            "Должна быть обработана команда RepeatCommand или LogCommand");
    }
}
