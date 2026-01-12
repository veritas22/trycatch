using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TryExp.Core;

/// <summary>
/// Обработчик очереди команд. Извлекает команды из очереди и выполняет их,
/// обрабатывая исключения через стратегии.
/// </summary>
public class CommandProcessor
{
    private readonly BlockingCollection<ICommand> _queue;
    private readonly ExceptionStrategyRegistry _strategyRegistry;
    private readonly ILogger<CommandProcessor> _logger;

    public CommandProcessor(
        BlockingCollection<ICommand> queue,
        ExceptionStrategyRegistry strategyRegistry,
        ILogger<CommandProcessor> logger)
    {
        _queue = queue;
        _strategyRegistry = strategyRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Добавляет команду в очередь на выполнение
    /// </summary>
    public void Enqueue(ICommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        _queue.Add(command);
        _logger.LogDebug("Command {CommandType} enqueued", command.GetType().Name);
    }

    /// <summary>
    /// Обрабатывает очередь команд. Извлекает команды и выполняет их.
    /// При возникновении исключения выбирает стратегию и добавляет команду восстановления в очередь.
    /// </summary>
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🚀 Начало обработки очереди команд");

        var tasks = Enumerable.Range(0, 4)
            .Select(i => Task.Run(async () => await ProcessQueueAsync(i, cancellationToken), cancellationToken))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task ProcessQueueAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("👷 Worker {WorkerId} запущен", workerId);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_queue.TryTake(out var command, 1000, cancellationToken))
                {
                    await ExecuteCommandAsync(command, workerId);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Критическая ошибка в worker {WorkerId}", workerId);
            }
        }

        _logger.LogInformation("👷 Worker {WorkerId} остановлен", workerId);
    }

    private async Task ExecuteCommandAsync(ICommand command, int workerId)
    {
        try
        {
            _logger.LogDebug("▶️ Worker {WorkerId}: Выполнение команды {CommandType}", 
                workerId, command.GetType().Name);

            // Выполняем команду - может выбросить любое исключение
            await Task.Run(() => command.Execute(), CancellationToken.None);

            _logger.LogDebug("✅ Worker {WorkerId}: Команда {CommandType} выполнена успешно", 
                workerId, command.GetType().Name);
        }
        catch (Exception ex)
        {
            // Перехватываем самое базовое исключение Exception
            _logger.LogWarning(ex, "⚠️ Worker {WorkerId}: Исключение при выполнении команды {CommandType}", 
                workerId, command.GetType().Name);

            // Выбираем стратегию обработки исключения
            var strategy = _strategyRegistry.SelectStrategy(command, ex);

            // Создаем команду восстановления и добавляем в очередь
            var recoveryCommand = strategy.RecoverCommand(command, ex);
            _queue.Add(recoveryCommand, CancellationToken.None);

            _logger.LogInformation("🔄 Worker {WorkerId}: Добавлена команда восстановления {RecoveryCommandType}", 
                workerId, recoveryCommand.GetType().Name);
        }
    }
}
