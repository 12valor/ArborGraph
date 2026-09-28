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
    }

    public ObservableCollection<DuplicateGroup> DuplicateGroups { get; }

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
}
