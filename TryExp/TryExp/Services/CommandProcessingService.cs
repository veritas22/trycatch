using TryExp.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TryExp.Services;

/// <summary>
/// Фоновый сервис для обработки очереди команд
/// </summary>
public class CommandProcessingService : BackgroundService
{
    private readonly CommandProcessor _processor;
    private readonly ILogger<CommandProcessingService> _logger;

    public CommandProcessingService(
        CommandProcessor processor,
        ILogger<CommandProcessingService> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Космическая битва: Сервис обработки команд запущен");
        
        try
        {
            await _processor.ProcessAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("⏹️ Сервис обработки команд остановлен (отмена операции)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Критическая ошибка в сервисе обработки команд");
            throw;
        }
    }
}
