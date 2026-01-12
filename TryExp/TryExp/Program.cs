using TryExp.Core;
using TryExp.Commands;
using TryExp.Strategies;
using TryExp.Services;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// ========== Настройка логирования ==========
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// ========== DI: Регистрация основных сервисов ==========
builder.Services.AddSingleton<BlockingCollection<ICommand>>(_ => new BlockingCollection<ICommand>(100));
builder.Services.AddSingleton<ExceptionStrategyRegistry>();
builder.Services.AddSingleton<CommandProcessor>();

// ========== DI: Регистрация стратегий обработки исключений ==========
// Стратегия по умолчанию - логирование
builder.Services.AddSingleton<IExceptionStrategy, LogStrategy>();

// Стратегия: повторить один раз, затем записать в лог
builder.Services.AddSingleton<IExceptionStrategy, RepeatOnceThenLogStrategy>();

// Стратегия: повторить два раза, затем записать в лог
builder.Services.AddSingleton<IExceptionStrategy, RepeatTwiceThenLogStrategy>();

// Стратегия: повторить два раза, затем создать FailedAfterTwoAttemptsCommand
builder.Services.AddSingleton<IExceptionStrategy, FailedAfterTwoAttemptsStrategy>();

// ========== DI: Регистрация команд игры ==========
builder.Services.AddTransient<MoveShipCommand>();
builder.Services.AddTransient<FireLaserCommand>();

// ========== DI: BackgroundService для обработки очереди ==========
builder.Services.AddHostedService<CommandProcessingService>();

// ========== Настройка API ==========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ========== Настройка HTTP pipeline ==========
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// ========== API: Добавление команд в очередь ==========
app.MapPost("/commands/{commandName}", async (
    string commandName,
    CommandProcessor processor,
    IServiceProvider services,
    ILogger<Program> logger) =>
{
    ICommand? command = commandName switch
    {
        "MoveShip" => services.GetRequiredService<MoveShipCommand>(),
        "FireLaser" => services.GetRequiredService<FireLaserCommand>(),
        _ => null
    };

    if (command == null)
    {
        logger.LogWarning("Неизвестная команда: {CommandName}", commandName);
        return Results.BadRequest($"Неизвестная команда: {commandName}");
    }

    processor.Enqueue(command);
    logger.LogInformation("✅ Команда {CommandName} добавлена в очередь", commandName);
    return Results.Ok(new { message = $"Команда {commandName} добавлена в очередь", commandName });
});

// ========== API: Просмотр логов ==========
app.MapGet("/logs", () =>
{
    var logPath = "logs/exceptions.log";
    if (!File.Exists(logPath))
    {
        return Results.Ok("Логи пусты");
    }

    var logs = File.ReadAllText(logPath);
    return Results.Ok(new { logs });
});

// ========== API: Очистка логов ==========
app.MapDelete("/logs", () =>
{
    var logPath = "logs/exceptions.log";
    if (File.Exists(logPath))
    {
        File.Delete(logPath);
        return Results.Ok("Логи очищены");
    }
    return Results.Ok("Логи уже пусты");
});

// ========== API: Информация о системе ==========
app.MapGet("/", () => Results.Ok(new
{
    title = "Космическая битва - Система обработки исключений",
    description = "Демонстрация различных стратегий обработки исключений",
    endpoints = new
    {
        addCommand = "POST /commands/{commandName}",
        viewLogs = "GET /logs",
        clearLogs = "DELETE /logs",
        commands = new[] { "MoveShip", "FireLaser" }
    }
}));

app.Run();
