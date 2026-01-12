using Microsoft.Extensions.Logging;
using TryExp.Strategies;

namespace TryExp.Core;

/// <summary>
/// Реестр стратегий обработки исключений.
/// Выбирает подходящую стратегию на основе типа исключения или типа команды.
/// </summary>
public class ExceptionStrategyRegistry
{
    private readonly Dictionary<Type, IExceptionStrategy> _exceptionStrategies = new();
    private readonly Dictionary<Type, IExceptionStrategy> _commandStrategies = new();
    private readonly IExceptionStrategy _defaultStrategy;
    private readonly ILogger<ExceptionStrategyRegistry> _logger;

    public ExceptionStrategyRegistry(
        IEnumerable<IExceptionStrategy> strategies,
        ILogger<ExceptionStrategyRegistry> logger)
    {
        _logger = logger;
        
        // Регистрируем все стратегии
        foreach (var strategy in strategies)
        {
            RegisterStrategy(strategy);
        }

        // Стратегия по умолчанию - логирование
        _defaultStrategy = strategies.OfType<LogStrategy>().FirstOrDefault()
            ?? throw new InvalidOperationException("LogStrategy must be registered");
    }

    private void RegisterStrategy(IExceptionStrategy strategy)
    {
        var strategyType = strategy.GetType();
        _logger.LogDebug("Регистрация стратегии: {StrategyType}", strategyType.Name);
        
        // Регистрируем по типу команды, если стратегия это поддерживает
        if (strategy is ICommandTypeStrategy commandStrategy)
        {
            foreach (var commandType in commandStrategy.SupportedCommandTypes)
            {
                _commandStrategies[commandType] = strategy;
                _logger.LogDebug("Зарегистрирована стратегия команды {Strategy} для {CommandType}", 
                    strategyType.Name, commandType.Name);
            }
        }
    }

    /// <summary>
    /// Выбирает подходящую стратегию обработки исключения.
    /// Приоритет: тип команды > стратегия по умолчанию
    /// </summary>
    public IExceptionStrategy SelectStrategy(ICommand command, Exception exception)
    {
        // Ищем по типу команды
        if (_commandStrategies.TryGetValue(command.GetType(), out var strategy))
        {
            _logger.LogDebug("Выбрана стратегия команды {Strategy} для {CommandType}", 
                strategy.GetType().Name, command.GetType().Name);
            return strategy;
        }

        // Используем стратегию по умолчанию
        _logger.LogDebug("Используется стратегия по умолчанию {Strategy}", _defaultStrategy.GetType().Name);
        return _defaultStrategy;
    }
}

/// <summary>
/// Интерфейс для стратегий, которые привязаны к конкретным типам команд
/// </summary>
public interface ICommandTypeStrategy
{
    IEnumerable<Type> SupportedCommandTypes { get; }
}
