using System.Collections.ObjectModel;
using DiskScope.Infrastructure;

namespace DiskScope.ViewModels;

public class LogItem
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public class SkippedItem
{
    public string Path { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Category { get; set; } = "Directory";
}

public class ScanLogViewModel : ObservableObject
{
    public ScanLogViewModel()
    {
        Logs = [];
        SkippedItems = [];
        AddLog("INFO", "ArborGraph initialized. Ready for scan.");
    }

    public ObservableCollection<LogItem> Logs { get; }
    public ObservableCollection<SkippedItem> SkippedItems { get; }

    public void AddLog(string level, string message, string detail = "")
    {
        try
        {
            if (Logs.Count >= 500)
            {
                Logs.RemoveAt(0);
            }

            Logs.Add(new LogItem
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Detail = detail
            });
        }
        catch { }
    }

    public void AddSkippedDirectory(string path, string reason)
    {
        if (SkippedItems.Count >= 500)
        {
            SkippedItems.RemoveAt(0);
        }

        SkippedItems.Add(new SkippedItem
        {
            Path = path,
            Reason = reason,
            Category = "Directory"
        });

        AddLog("WARN", $"Skipped directory: {path}", reason);
    }

    public void AddSkippedFile(string path, string reason)
    {
        if (SkippedItems.Count >= 500)
        {
            SkippedItems.RemoveAt(0);
        }

        SkippedItems.Add(new SkippedItem
        {
            Path = path,
            Reason = reason,
            Category = "File"
        });

        AddLog("WARN", $"Skipped file: {path}", reason);
    }

    public void ClearLogs()
    {
        Logs.Clear();
        SkippedItems.Clear();
    }
}
