using TryExp.Core;
using Microsoft.Extensions.Logging;

namespace TryExp.Commands;

/// <summary>
/// Команда для записи информации об исключении в лог-файл
/// </summary>
public class LogCommand : ICommand
{
    private readonly ICommand _failedCommand;
    private readonly Exception _exception;
    private readonly string _logPath;
    private readonly ILogger<LogCommand>? _logger;

    public LogCommand(ICommand failedCommand, Exception exception, string logPath = "logs/exceptions.log", ILogger<LogCommand>? logger = null)
    {
        _failedCommand = failedCommand ?? throw new ArgumentNullException(nameof(failedCommand));
        _exception = exception ?? throw new ArgumentNullException(nameof(exception));
        _logPath = logPath;
        _logger = logger;
    }

    public void Execute()
    {
        try
        {
            var logDirectory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                           $"Команда={_failedCommand.GetType().Name}, " +
                           $"Исключение={_exception.GetType().Name}, " +
                           $"Сообщение={_exception.Message}\n";

            File.AppendAllText(_logPath, logMessage);
            
            _logger?.LogInformation("📝 Исключение записано в лог: {Message}", _exception.Message);
            Console.WriteLine($"📝 Исключение записано в лог: {_exception.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Ошибка при записи в лог");
            throw;
        }
    }
}

/// <summary>
/// Команда-обертка для повторного выполнения команды, которая выбросила исключение
/// </summary>
public class RepeatCommand : ICommand
{
    protected readonly ICommand _originalCommand;
    protected readonly ILogger<RepeatCommand>? _logger;

    public RepeatCommand(ICommand originalCommand, ILogger<RepeatCommand>? logger = null)
    {
        _originalCommand = originalCommand ?? throw new ArgumentNullException(nameof(originalCommand));
        _logger = logger;
    }

    public virtual void Execute()
    {
        _logger?.LogInformation("🔄 Повторное выполнение команды {CommandType}", _originalCommand.GetType().Name);
        Console.WriteLine($"🔄 Повторное выполнение команды {_originalCommand.GetType().Name}");
        _originalCommand.Execute();
    }

    /// <summary>
    /// Получает оригинальную команду, если она обернута в RepeatCommand
    /// </summary>
    public static ICommand Unwrap(ICommand command)
    {
        return command is RepeatCommand repeatCommand 
            ? Unwrap(repeatCommand._originalCommand) 
            : command;
    }
}

/// <summary>
/// Команда для повторного выполнения с отслеживанием количества попыток
/// </summary>
public class RepeatTwiceCommand : RepeatCommand
{
    private int _attemptsLeft;

    public RepeatTwiceCommand(ICommand originalCommand, int attempts = 2, ILogger<RepeatCommand>? logger = null) 
        : base(originalCommand, logger)
    {
        _attemptsLeft = attempts;
    }

    public int AttemptsLeft => _attemptsLeft;

    public override void Execute()
    {
        _logger?.LogInformation("🔄 Попытка выполнения команды {CommandType} (осталось попыток: {AttemptsLeft})", 
            _originalCommand.GetType().Name, _attemptsLeft);
        
        Console.WriteLine($"🔄 Попытка выполнения команды {_originalCommand.GetType().Name} (осталось попыток: {_attemptsLeft})");
        
        _originalCommand.Execute();
        _attemptsLeft--;
    }
}

/// <summary>
/// Команда, которая выбрасывает исключение после двух неудачных попыток
/// Используется для маркировки команды, которая не смогла выполниться после двух попыток
/// </summary>
public class FailedAfterTwoAttemptsCommand : ICommand
{
    private readonly ICommand _originalCommand;
    private readonly ILogger<FailedAfterTwoAttemptsCommand>? _logger;

    public FailedAfterTwoAttemptsCommand(ICommand originalCommand, ILogger<FailedAfterTwoAttemptsCommand>? logger = null)
    {
        _originalCommand = originalCommand ?? throw new ArgumentNullException(nameof(originalCommand));
        _logger = logger;
    }

    public void Execute()
    {
        _logger?.LogWarning("❌ Команда {CommandType} не смогла выполниться после двух попыток", 
            _originalCommand.GetType().Name);
        
        throw new InvalidOperationException(
            $"Команда {_originalCommand.GetType().Name} не смогла выполниться после двух попыток повторного выполнения.");
    }
}
