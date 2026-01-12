using TryExp.Core;
using Microsoft.Extensions.Logging;

namespace TryExp.Commands;

/// <summary>
/// Команда перемещения корабля в космической битве
/// </summary>
public class MoveShipCommand : ICommand
{
    private static readonly Random _random = new();
    private readonly ILogger<MoveShipCommand>? _logger;

    public MoveShipCommand(ILogger<MoveShipCommand>? logger = null)
    {
        _logger = logger;
    }

    public void Execute()
    {
        // Симуляция случайной ошибки (25% вероятность)
        if (_random.Next(4) == 0)
        {
            throw new InvalidOperationException("🚀 Столкновение! Корабль не может переместиться.");
        }

        _logger?.LogInformation("✅ Корабль успешно перемещён");
        Console.WriteLine("✅ Корабль перемещён");
    }
}

/// <summary>
/// Команда выстрела лазером в космической битве
/// </summary>
public class FireLaserCommand : ICommand
{
    private static readonly Random _random = new();
    private readonly ILogger<FireLaserCommand>? _logger;

    public FireLaserCommand(ILogger<FireLaserCommand>? logger = null)
    {
        _logger = logger;
    }

    public void Execute()
    {
        // Симуляция случайной ошибки (33% вероятность)
        if (_random.Next(3) == 0)
        {
            throw new ArgumentException("🔥 Нет патронов! Лазер не может выстрелить.");
        }

        _logger?.LogInformation("✅ Лазер выстрелил!");
        Console.WriteLine("✅ Лазер выстрелил!");
    }
}
