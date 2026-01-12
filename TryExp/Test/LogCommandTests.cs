using TryExp.Commands;
using TryExp.Core;
using Xunit;

namespace Test;

/// <summary>
/// Тесты для команды LogCommand - запись информации об исключении в лог
/// </summary>
public class LogCommandTests
{
    private readonly string _testLogPath;

    public LogCommandTests()
    {
        _testLogPath = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");
    }

    [Fact]
    public void Execute_ShouldCreateLogFile_WhenFileDoesNotExist()
    {
        // Arrange
        if (File.Exists(_testLogPath))
        {
            File.Delete(_testLogPath);
        }

        var testCommand = new TestFailingCommand("TestCommand");
        var exception = new InvalidOperationException("Test exception message");
        var logCommand = new LogCommand(testCommand, exception, _testLogPath);

        // Act
        logCommand.Execute();

        // Assert
        Assert.True(File.Exists(_testLogPath), "Лог-файл должен быть создан");
    }

    [Fact]
    public void Execute_ShouldAppendToLogFile_WhenFileExists()
    {
        // Arrange
        var testCommand = new TestFailingCommand("TestCommand");
        var exception1 = new InvalidOperationException("First exception");
        var exception2 = new ArgumentException("Second exception");

        var logCommand1 = new LogCommand(testCommand, exception1, _testLogPath);
        var logCommand2 = new LogCommand(testCommand, exception2, _testLogPath);

        // Act
        logCommand1.Execute();
        logCommand2.Execute();

        // Assert
        var logContent = File.ReadAllText(_testLogPath);
        Assert.Contains("First exception", logContent);
        Assert.Contains("Second exception", logContent);
        Assert.Contains("TestCommand", logContent);
    }

    [Fact]
    public void Execute_ShouldLogCorrectInformation()
    {
        // Arrange
        var testCommand = new TestFailingCommand("MyTestCommand");
        var exception = new InvalidOperationException("Test error message");
        var logCommand = new LogCommand(testCommand, exception, _testLogPath);

        // Act
        logCommand.Execute();

        // Assert
        var logContent = File.ReadAllText(_testLogPath);
        Assert.Contains("MyTestCommand", logContent);
        Assert.Contains("InvalidOperationException", logContent);
        Assert.Contains("Test error message", logContent);
        Assert.Matches(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]", logContent);
    }

    [Fact]
    public void Execute_ShouldCreateDirectory_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), $"test_dir_{Guid.NewGuid()}");
        var logPath = Path.Combine(testDir, "nested", "log.log");

        if (Directory.Exists(testDir))
        {
            Directory.Delete(testDir, true);
        }

        var testCommand = new TestFailingCommand("TestCommand");
        var exception = new Exception("Test");
        var logCommand = new LogCommand(testCommand, exception, logPath);

        // Act
        logCommand.Execute();

        // Assert
        Assert.True(Directory.Exists(Path.GetDirectoryName(logPath)), "Директория должна быть создана");
        Assert.True(File.Exists(logPath), "Лог-файл должен быть создан");

        // Cleanup
        Directory.Delete(testDir, true);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenFailedCommandIsNull()
    {
        // Arrange & Act & Assert
        var exception = new Exception("Test");
        Assert.Throws<ArgumentNullException>(() => new LogCommand(null!, exception, _testLogPath));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenExceptionIsNull()
    {
        // Arrange & Act & Assert
        var testCommand = new TestFailingCommand("Test");
        Assert.Throws<ArgumentNullException>(() => new LogCommand(testCommand, null!, _testLogPath));
    }

    [Fact]
    public void Execute_ShouldThrowException_WhenLogPathIsInvalid()
    {
        // Arrange
        var invalidPath = "Z:\\Invalid\\Path\\That\\Does\\Not\\Exist\\log.log";
        var testCommand = new TestFailingCommand("Test");
        var exception = new Exception("Test");
        var logCommand = new LogCommand(testCommand, exception, invalidPath);

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => logCommand.Execute());
    }

    // Cleanup
    ~LogCommandTests()
    {
        if (File.Exists(_testLogPath))
        {
            try
            {
                File.Delete(_testLogPath);
            }
            catch { }
        }
    }
}

/// <summary>
/// Тестовая команда, которая всегда выбрасывает исключение
/// </summary>
internal class TestFailingCommand : ICommand
{
    private readonly string _name;

    public TestFailingCommand(string name)
    {
        _name = name;
    }

    public void Execute()
    {
        throw new InvalidOperationException($"Command {_name} failed");
    }
}
