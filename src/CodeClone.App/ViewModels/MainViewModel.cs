using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodeClone.App.Services;
using CodeClone.Domain;

namespace CodeClone.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly CodeCloneService _codeCloneService = new();
    private readonly SnapshotService _snapshotService = new();
    private AnalyzeResponse? _currentResponse;
    private Snapshot? _currentSnapshot;
    private Snapshot? _previousSnapshot;
    private string? _repoRoot;

    [ObservableProperty]
    private string _statusText = "Open a repository to begin";

    [ObservableProperty]
    private AnalysisStatus? _analysisStatus;

    [ObservableProperty]
    private int _totalDiagnostics;

    [ObservableProperty]
    private int _filesAnalyzed;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private string _currentFileName = "";

    [ObservableProperty]
    private FileTreeItem? _selectedFile;

    [ObservableProperty]
    private DiagnosticItem? _selectedDiagnostic;

    [ObservableProperty]
    private HotspotItem? _selectedHotspot;

    // === RISK SCORE (The Headline Metric) ===
    [ObservableProperty]
    private RiskLevel _riskLevel = RiskLevel.Low;

    [ObservableProperty]
    private int _riskScore;

    [ObservableProperty]
    private string _riskTrend = "";

    [ObservableProperty]
    private int _diagnosticDelta;

    [ObservableProperty]
    private bool _hasComparison;

    public ObservableCollection<FileTreeItem> FileTree { get; } = [];
    public ObservableCollection<CodeLine> CodeLines { get; } = [];
    public ObservableCollection<DiagnosticItem> Diagnostics { get; } = [];
    public ObservableCollection<HotspotItem> Hotspots { get; } = [];

    public string RiskColor => RiskLevel switch
    {
        RiskLevel.Critical => "#D32F2F",
        RiskLevel.High => "#F57C00",
        RiskLevel.Medium => "#FBC02D",
        RiskLevel.Low => "#388E3C",
        _ => "#9E9E9E"
    };

    public string StatusColor => AnalysisStatus switch
    {
        Domain.AnalysisStatus.OK => "#4CAF50",
        Domain.AnalysisStatus.PARTIAL => "#FF9800",
        Domain.AnalysisStatus.FAIL => "#F44336",
        _ => "#9E9E9E"
    };

    public string TrendIcon => RiskTrend switch
    {
        "improving" => "↓",
        "worsening" => "↑",
        _ => "→"
    };

    public string TrendColor => RiskTrend switch
    {
        "improving" => "#4CAF50",
        "worsening" => "#F44336",
        _ => "#9E9E9E"
    };

    [RelayCommand]
    private async Task OpenRepoAsync()
    {
        var result = await FolderPicker.Default.PickAsync(default);
        if (result.IsSuccessful && result.Folder is not null)
        {
            _repoRoot = result.Folder.Path;
            await LoadRepoAsync(_repoRoot);
        }
    }

    [RelayCommand]
    private async Task RunAnalysisAsync()
    {
        if (_repoRoot is null) return;

        IsAnalyzing = true;
        StatusText = "Running analysis...";

        try
        {
            // Get previous snapshot for comparison
            _previousSnapshot = await _snapshotService.GetLatestSnapshotAsync(_repoRoot);

            var result = await _codeCloneService.AnalyzeAsync(_repoRoot);

            if (result.IsSuccess && result.Response is not null)
            {
                _currentResponse = result.Response;

                // Create and save snapshot
                StatusText = "Saving snapshot...";
                _currentSnapshot = await _snapshotService.CreateSnapshotAsync(_repoRoot, result.Response);

                LoadAnalysisResults(result.Response, _currentSnapshot);

                // Compare with previous if available
                if (_previousSnapshot is not null)
                {
                    var comparison = _snapshotService.Compare(_previousSnapshot, _currentSnapshot);
                    UpdateComparison(comparison);
                }

                StatusText = $"Analysis complete • Risk: {_currentSnapshot.RiskScore.Level}";
            }
            else
            {
                StatusText = result.Error ?? "Analysis failed";
            }
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    private async Task ViewHistoryAsync()
    {
        if (_repoRoot is null) return;

        var snapshots = await _snapshotService.GetSnapshotsAsync(_repoRoot);
        // TODO: Open history view/dialog
        StatusText = $"Found {snapshots.Count} historical snapshots";
    }

    partial void OnSelectedFileChanged(FileTreeItem? value)
    {
        if (value is not null && !value.IsDirectory)
        {
            LoadFileContent(value.FullPath);
        }
    }

    partial void OnSelectedDiagnosticChanged(DiagnosticItem? value)
    {
        if (value?.Diagnostic.File is not null && _repoRoot is not null)
        {
            var fullPath = Path.Combine(_repoRoot, value.Diagnostic.File);
            LoadFileContent(fullPath);
        }
    }

    partial void OnSelectedHotspotChanged(HotspotItem? value)
    {
        if (value?.Hotspot.File is not null && _repoRoot is not null)
        {
            var fullPath = Path.Combine(_repoRoot, value.Hotspot.File);
            LoadFileContent(fullPath);
        }
    }

    private async Task LoadRepoAsync(string path)
    {
        FileTree.Clear();
        CodeLines.Clear();
        Diagnostics.Clear();
        Hotspots.Clear();
        _currentResponse = null;
        _currentSnapshot = null;
        HasComparison = false;

        var rootItem = new FileTreeItem
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            IsDirectory = true,
            IsExpanded = true
        };

        await PopulateDirectoryAsync(rootItem, path, maxDepth: 3);
        FileTree.Add(rootItem);

        // Load latest snapshot if available
        var latest = await _snapshotService.GetLatestSnapshotAsync(path);
        if (latest is not null)
        {
            StatusText = $"Loaded: {path} • Last analyzed: {latest.Timestamp:g}";
            RiskLevel = latest.RiskScore.Level;
            RiskScore = latest.RiskScore.Score;
        }
        else
        {
            StatusText = $"Loaded: {path} • No previous analysis";
            RiskLevel = RiskLevel.Low;
            RiskScore = 0;
        }

        AnalysisStatus = null;
    }

    private async Task PopulateDirectoryAsync(FileTreeItem parent, string path, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth >= maxDepth) return;

        try
        {
            var dirs = Directory.GetDirectories(path)
                .Where(d => !IsIgnoredDirectory(d))
                .OrderBy(d => Path.GetFileName(d));

            var files = Directory.GetFiles(path)
                .Where(f => IsSupportedFile(f))
                .OrderBy(f => Path.GetFileName(f));

            foreach (var dir in dirs)
            {
                var item = new FileTreeItem
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    IsDirectory = true
                };
                parent.Children.Add(item);
                await PopulateDirectoryAsync(item, dir, maxDepth, currentDepth + 1);
            }

            foreach (var file in files)
            {
                parent.Children.Add(new FileTreeItem
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    IsDirectory = false
                });
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip inaccessible directories
        }
    }

    private static bool IsIgnoredDirectory(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith('.') ||
               name is "node_modules" or "__pycache__" or "venv" or ".venv" or
                       "bin" or "obj" or "dist" or "build" or ".git";
    }

    private static bool IsSupportedFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".py" or ".js" or ".ts" or ".cs" or ".java" or ".go" or
                      ".rs" or ".rb" or ".php" or ".c" or ".cpp" or ".h";
    }

    private void LoadAnalysisResults(AnalyzeResponse response, Snapshot snapshot)
    {
        AnalysisStatus = response.Status;
        TotalDiagnostics = response.Summary.TotalDiagnostics;
        FilesAnalyzed = response.Summary.TotalFilesAnalyzed;

        // Update risk score
        RiskLevel = snapshot.RiskScore.Level;
        RiskScore = snapshot.RiskScore.Score;

        // Load diagnostics
        Diagnostics.Clear();
        foreach (var diag in response.Diagnostics)
        {
            Diagnostics.Add(new DiagnosticItem { Diagnostic = diag });
        }

        // Load hotspots
        Hotspots.Clear();
        foreach (var hotspot in snapshot.Hotspots)
        {
            Hotspots.Add(new HotspotItem { Hotspot = hotspot });
        }

        // Update file tree with diagnostic counts
        UpdateFileTreeDiagnostics(response);
    }

    private void UpdateComparison(SnapshotComparison comparison)
    {
        HasComparison = true;
        DiagnosticDelta = comparison.DiagnosticDelta;

        RiskTrend = comparison.Trend switch
        {
            TrendDirection.Improving => "improving",
            TrendDirection.Worsening => "worsening",
            _ => "stable"
        };

        OnPropertyChanged(nameof(TrendIcon));
        OnPropertyChanged(nameof(TrendColor));
    }

    private void UpdateFileTreeDiagnostics(AnalyzeResponse response)
    {
        var fileDiagnostics = response.Diagnostics
            .Where(d => d.File is not null)
            .GroupBy(d => d.File!)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var item in FlattenTree(FileTree))
        {
            if (!item.IsDirectory && _repoRoot is not null)
            {
                var relativePath = Path.GetRelativePath(_repoRoot, item.FullPath);
                if (fileDiagnostics.TryGetValue(relativePath, out var count))
                {
                    item.DiagnosticCount = count;
                }
            }
        }
    }

    private static IEnumerable<FileTreeItem> FlattenTree(IEnumerable<FileTreeItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in FlattenTree(item.Children))
            {
                yield return child;
            }
        }
    }

    private void LoadFileContent(string path)
    {
        CodeLines.Clear();
        CurrentFileName = Path.GetFileName(path);

        if (!File.Exists(path)) return;

        try
        {
            var lines = File.ReadAllLines(path);
            var uncoveredLines = GetUncoveredLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                var lineNum = i + 1; // 1-based line numbers
                var status = uncoveredLines.Contains(lineNum)
                    ? CoverageStatus.Uncovered
                    : CoverageStatus.None;

                CodeLines.Add(new CodeLine
                {
                    LineNumber = lineNum,
                    Text = lines[i],
                    Status = status,
                    HasDiagnostic = HasDiagnosticAtLine(path, lineNum)
                });
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading file: {ex.Message}";
        }
    }

    private HashSet<int> GetUncoveredLines(string fullPath)
    {
        if (_currentResponse is null || _repoRoot is null)
            return [];

        var relativePath = Path.GetRelativePath(_repoRoot, fullPath);
        var uncovered = new HashSet<int>();

        foreach (var diag in _currentResponse.Diagnostics)
        {
            if (diag.Code == DiagnosticCodes.LineUncovered &&
                string.Equals(diag.File, relativePath, StringComparison.OrdinalIgnoreCase) &&
                diag.Line.HasValue)
            {
                var startLine = diag.Line.Value;
                var endLine = diag.EndLine ?? startLine;
                for (int line = startLine; line <= endLine; line++)
                {
                    uncovered.Add(line);
                }
            }
        }

        return uncovered;
    }

    private bool HasDiagnosticAtLine(string fullPath, int lineNum)
    {
        if (_currentResponse is null || _repoRoot is null)
            return false;

        var relativePath = Path.GetRelativePath(_repoRoot, fullPath);
        return _currentResponse.Diagnostics.Any(d =>
            string.Equals(d.File, relativePath, StringComparison.OrdinalIgnoreCase) &&
            d.Line == lineNum);
    }
}
