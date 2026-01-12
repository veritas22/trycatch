using TryExp.Core;
using TryExp.Commands;
using Microsoft.Extensions.Logging;

namespace TryExp.Strategies;

/// <summary>
/// Стратегия обработки исключений: записывает информацию об исключении в лог
/// </summary>
public class LogStrategy : IExceptionStrategy
{
    private readonly ILogger<LogStrategy>? _logger;

    public LogStrategy(ILogger<LogStrategy>? logger = null)
    {
        _logger = logger;
    }

    public ICommand RecoverCommand(ICommand failedCommand, Exception exception)
    {
        _logger?.LogDebug("Создание LogCommand для команды {CommandType}", failedCommand.GetType().Name);
        return new LogCommand(failedCommand, exception);
    }
}

/// <summary>
/// Стратегия обработки исключений: при первом исключении повторяет команду,
/// при повторном исключении записывает в лог
/// </summary>
public class RepeatOnceThenLogStrategy : IExceptionStrategy
{
    private readonly ILogger<RepeatOnceThenLogStrategy>? _logger;

    public RepeatOnceThenLogStrategy(ILogger<RepeatOnceThenLogStrategy>? logger = null)
    {
        _logger = logger;
    }

    public ICommand RecoverCommand(ICommand failedCommand, Exception exception)
    {
        // Если команда уже является RepeatCommand, значит это повторная попытка
        // В этом случае логируем исключение
        if (failedCommand is RepeatCommand)
        {
            _logger?.LogInformation("Команда уже была повторена, создаём LogCommand");
            var originalCommand = RepeatCommand.Unwrap(failedCommand);
            return new LogCommand(originalCommand, exception);
        }

        // Первое исключение - повторяем команду
        _logger?.LogInformation("Первое исключение, создаём RepeatCommand для повторной попытки");
        return new RepeatCommand(failedCommand);
    }
}

/// <summary>
/// Стратегия обработки исключений: повторяет команду два раза, затем записывает в лог
/// </summary>
public class RepeatTwiceThenLogStrategy : IExceptionStrategy
{
    private readonly ILogger<RepeatTwiceThenLogStrategy>? _logger;

    public RepeatTwiceThenLogStrategy(ILogger<RepeatTwiceThenLogStrategy>? logger = null)
    {
        _logger = logger;
    }

    public ICommand RecoverCommand(ICommand failedCommand, Exception exception)
    {
        // Если команда уже является RepeatTwiceCommand
        if (failedCommand is RepeatTwiceCommand repeatTwiceCommand)
        {
            // Если попытки закончились, логируем
            if (repeatTwiceCommand.AttemptsLeft <= 0)
            {
                _logger?.LogInformation("Попытки закончились, создаём LogCommand");
                var originalCommand = RepeatCommand.Unwrap(repeatTwiceCommand);
                return new LogCommand(originalCommand, exception);
            }

            // Если попытки ещё есть, продолжаем повторять
            _logger?.LogInformation("Продолжаем повторять команду (осталось попыток: {AttemptsLeft})", 
                repeatTwiceCommand.AttemptsLeft);
            return repeatTwiceCommand;
        }

        // Первое исключение - создаём RepeatTwiceCommand с двумя попытками
        _logger?.LogInformation("Первое исключение, создаём RepeatTwiceCommand с двумя попытками");
        return new RepeatTwiceCommand(failedCommand, attempts: 2);
    }
}

/// <summary>
/// Стратегия обработки исключений: повторяет команду два раза,
/// затем создаёт FailedAfterTwoAttemptsCommand для маркировки неудачи
/// </summary>
public class FailedAfterTwoAttemptsStrategy : IExceptionStrategy
{
    private readonly ILogger<FailedAfterTwoAttemptsStrategy>? _logger;

    public FailedAfterTwoAttemptsStrategy(ILogger<FailedAfterTwoAttemptsStrategy>? logger = null)
    {
        _logger = logger;
    }

    public ICommand RecoverCommand(ICommand failedCommand, Exception exception)
    {
        // Если команда уже является RepeatTwiceCommand и попытки закончились
        if (failedCommand is RepeatTwiceCommand repeatTwiceCommand && repeatTwiceCommand.AttemptsLeft <= 0)
        {
            _logger?.LogWarning("Команда не смогла выполниться после двух попыток, создаём FailedAfterTwoAttemptsCommand");
            var originalCommand = RepeatCommand.Unwrap(repeatTwiceCommand);
            return new FailedAfterTwoAttemptsCommand(originalCommand);
        }

        // Если команда уже является RepeatTwiceCommand, но попытки ещё есть
        if (failedCommand is RepeatTwiceCommand)
        {
            _logger?.LogInformation("Продолжаем повторять команду");
            return failedCommand;
        }

        // Первое исключение - создаём RepeatTwiceCommand с двумя попытками
        _logger?.LogInformation("Первое исключение, создаём RepeatTwiceCommand с двумя попытками");
        return new RepeatTwiceCommand(failedCommand, attempts: 2);
    }
}
