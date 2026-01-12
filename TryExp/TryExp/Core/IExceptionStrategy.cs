namespace TryExp.Core;

/// <summary>
/// Стратегия обработки исключений. Определяет, какая команда будет создана для обработки исключения.
/// </summary>
public interface IExceptionStrategy
{
    /// <summary>
    /// Создает команду для обработки исключения, возникшего при выполнении команды.
    /// </summary>
    /// <param name="failedCommand">Команда, которая выбросила исключение</param>
    /// <param name="exception">Выброшенное исключение</param>
    /// <returns>Команда для обработки исключения</returns>
    ICommand RecoverCommand(ICommand failedCommand, Exception exception);
}
