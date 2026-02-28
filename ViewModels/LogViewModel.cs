using System.Collections.ObjectModel;
using System.Windows.Input;
using Clash_WPF.Helpers;
using Clash_WPF.Models;

namespace Clash_WPF.ViewModels;

public class LogEntryItem
{
    public string Time { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class LogViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private CancellationTokenSource? _logCts;
    private const int MaxLogEntries = 1000;

    public ObservableCollection<LogEntryItem> Logs { get; } = [];

    private string _logLevel = "info";
    public string LogLevel
    {
        get => _logLevel;
        set
        {
            if (SetProperty(ref _logLevel, value))
                RestartLogStream();
        }
    }

    public ICommand ClearLogsCommand { get; }
    public ICommand SetLogLevelCommand { get; }

    public LogViewModel(MainViewModel main)
    {
        _main = main;
        ClearLogsCommand = RelayCommand.Create(() => Logs.Clear());
        SetLogLevelCommand = new RelayCommand(p =>
        {
            if (p is string level) LogLevel = level;
        });
    }

    public override Task ActivateAsync()
    {
        RestartLogStream();
        return Task.CompletedTask;
    }

    public override void Deactivate()
    {
        _logCts?.Cancel();
        _logCts = null;
    }

    private void RestartLogStream()
    {
        _logCts?.Cancel();
        _logCts = new CancellationTokenSource();
        var ct = _logCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var log in _main.Api.StreamLogsAsync(_logLevel, ct))
                {
                    if (log is null) continue;
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        Logs.Add(new LogEntryItem
                        {
                            Time = DateTime.Now.ToString("HH:mm:ss"),
                            Level = log.Type.ToUpperInvariant(),
                            Message = log.Payload,
                        });

                        while (Logs.Count > MaxLogEntries)
                            Logs.RemoveAt(0);
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch { /* stream ended */ }
        }, ct);
    }
}
