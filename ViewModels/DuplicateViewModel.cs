using System.Collections.ObjectModel;
using System.Windows.Input;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public class DuplicateViewModel : ObservableObject
{
    private readonly DuplicateAnalyzer _analyzer;
    private readonly FileActionService _fileActionService;

    private bool _isAnalyzing;
    private string _statusMessage = "Ready to analyze duplicates across indexed files.";
    private DuplicateGroup? _selectedGroup;
    private FileRecord? _selectedFile;
    private long _totalWastedBytes;
    private int _totalDuplicateFiles;
    private CancellationTokenSource? _cts;

    public DuplicateViewModel(DuplicateAnalyzer analyzer, FileActionService fileActionService)
    {
        _analyzer = analyzer;
        _fileActionService = fileActionService;
        DuplicateGroups = [];

        StartAnalysisCommand = new RelayCommand(async _ => await RunAnalysisAsync(), _ => !IsAnalyzing);
        CancelAnalysisCommand = new RelayCommand(_ => CancelAnalysis(), _ => IsAnalyzing);

        OpenFileCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFile(SelectedFile.Path); });
        OpenFileLocationCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.OpenFileLocation(SelectedFile.Path); });
        CopyPathCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.CopyPath(SelectedFile.Path); });
        ShowPropertiesCommand = new RelayCommand(_ => { if (SelectedFile != null) _fileActionService.ShowProperties(SelectedFile.Path); });
        ExportCsvCommand = new RelayCommand(async _ => await ExportCsvAsync(), _ => DuplicateGroups.Count > 0);
    }

    public ObservableCollection<DuplicateGroup> DuplicateGroups { get; }
    public ICommand ExportCsvCommand { get; }

    public DuplicateGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                SelectedFile = _selectedGroup?.Files.FirstOrDefault();
            }
        }
    }

    public FileRecord? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set
        {
            if (SetProperty(ref _isAnalyzing, value))
            {
                (StartAnalysisCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CancelAnalysisCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public long TotalWastedBytes
    {
        get => _totalWastedBytes;
        set
        {
            if (SetProperty(ref _totalWastedBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotalWasted));
            }
        }
    }

    public int TotalDuplicateFiles
    {
        get => _totalDuplicateFiles;
        set => SetProperty(ref _totalDuplicateFiles, value);
    }

    public string FormattedTotalWasted => SizeFormatter.Format(_totalWastedBytes);

    public ICommand StartAnalysisCommand { get; }
    public ICommand CancelAnalysisCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }

    public async Task RunAnalysisAsync()
    {
        if (IsAnalyzing) return;

        IsAnalyzing = true;
        _cts = new CancellationTokenSource();
        DuplicateGroups.Clear();
        TotalWastedBytes = 0;
        TotalDuplicateFiles = 0;

        var progress = new Progress<string>(msg => StatusMessage = msg);

        try
        {
            var results = await _analyzer.FindDuplicatesAsync(
                minSize: 1024,
                maxCandidates: 1000,
                statusProgress: progress,
                cancellationToken: _cts.Token);

            foreach (var g in results)
            {
                DuplicateGroups.Add(g);
            }

            TotalWastedBytes = results.Sum(g => g.WastedBytes);
            TotalDuplicateFiles = results.Sum(g => g.FileCount);
            SelectedGroup = DuplicateGroups.FirstOrDefault();
            (ExportCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Duplicate analysis was stopped.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Analysis error: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
            try { _cts?.Dispose(); } catch { }
            _cts = null;
        }
    }

    public void CancelAnalysis()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException) { }
    }

    public async Task ExportCsvAsync()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = $"diskscope_duplicates_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Title = "Export Duplicates to CSV"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var exporter = new ExportService();
                await exporter.ExportDuplicatesToCsvAsync(DuplicateGroups, sfd.FileName);
                var res = System.Windows.MessageBox.Show(
                    $"Exported {DuplicateGroups.Count:N0} duplicate groups to:\n{sfd.FileName}\n\nWould you like to open it now?",
                    "Export Successful",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                if (res == System.Windows.MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
