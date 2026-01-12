namespace TryExp.Core;

/// <summary>
/// Базовый интерфейс для всех команд в системе
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Выполняет команду. Может выбросить исключение при ошибке выполнения.
    /// </summary>
    void Execute();
}
