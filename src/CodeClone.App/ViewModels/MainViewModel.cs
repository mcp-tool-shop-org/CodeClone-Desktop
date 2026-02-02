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
    private readonly InsightEngine _insightEngine = new();
    private AnalyzeResponse? _currentResponse;
    private Snapshot? _currentSnapshot;
    private Snapshot? _previousSnapshot;
    private SnapshotComparison? _comparison;
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

    [ObservableProperty]
    private ActionItemViewModel? _selectedAction;

    // === VIEW STATE ===
    [ObservableProperty]
    private bool _showDashboard;

    [ObservableProperty]
    private bool _hasAnalysis;

    [ObservableProperty]
    private bool _hasRepo;

    [ObservableProperty]
    private string _repoName = "";

    [ObservableProperty]
    private bool _showScoreExplainer;

    [ObservableProperty]
    private string _scoreExplanation = "";

    [ObservableProperty]
    private int _currentFileIssueCount;

    public bool ShowWelcome => HasRepo && !HasAnalysis;
    public bool ShowDetailsView => HasAnalysis && !ShowDashboard;
    public bool HasCurrentFile => !string.IsNullOrEmpty(CurrentFileName);

    public ObservableCollection<ScoreFactorViewModel> ScoreFactors { get; } = [];

    // === INSIGHT DASHBOARD ===
    [ObservableProperty]
    private string _insightHeadline = "";

    [ObservableProperty]
    private string _insightSubtext = "";

    [ObservableProperty]
    private string _insightWhyItMatters = "";

    [ObservableProperty]
    private InsightSeverity _insightSeverity = InsightSeverity.Info;

    // === METRICS ===
    [ObservableProperty]
    private double _duplicationPercent;

    [ObservableProperty]
    private int _hotspotCount;

    [ObservableProperty]
    private int _affectedFiles;

    [ObservableProperty]
    private int _affectedLines;

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

    // === TREND INSIGHT ===
    [ObservableProperty]
    private string _trendHeadline = "";

    [ObservableProperty]
    private string _trendSubtext = "";

    [ObservableProperty]
    private bool _hasTrendInsight;

    public ObservableCollection<FileTreeItem> FileTree { get; } = [];
    public ObservableCollection<CodeLine> CodeLines { get; } = [];
    public ObservableCollection<DiagnosticItem> Diagnostics { get; } = [];
    public ObservableCollection<HotspotItem> Hotspots { get; } = [];
    public ObservableCollection<ActionItemViewModel> ActionItems { get; } = [];

    public string RiskColor => RiskLevel switch
    {
        RiskLevel.Critical => "#D32F2F",
        RiskLevel.High => "#F57C00",
        RiskLevel.Medium => "#FBC02D",
        RiskLevel.Low => "#388E3C",
        _ => "#9E9E9E"
    };

    public string InsightColor => InsightSeverity switch
    {
        InsightSeverity.Critical => "#D32F2F",
        InsightSeverity.Warning => "#F57C00",
        _ => "#388E3C"
    };

    public string InsightBackgroundColor => InsightSeverity switch
    {
        InsightSeverity.Critical => "#FFEBEE",
        InsightSeverity.Warning => "#FFF3E0",
        _ => "#E8F5E9"
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
    private async Task AssessRiskAsync()
    {
        if (_repoRoot is null) return;

        IsAnalyzing = true;
        StatusText = "Assessing risk...";

        try
        {
            // Get previous snapshot for comparison
            _previousSnapshot = await _snapshotService.GetLatestSnapshotAsync(_repoRoot);

            var result = await _codeCloneService.AnalyzeAsync(_repoRoot);

            if (result.IsSuccess && result.Response is not null)
            {
                _currentResponse = result.Response;

                // Create and save snapshot
                StatusText = "Generating insights...";
                _currentSnapshot = await _snapshotService.CreateSnapshotAsync(_repoRoot, result.Response);

                // Compare with previous if available
                _comparison = _previousSnapshot is not null
                    ? _snapshotService.Compare(_previousSnapshot, _currentSnapshot)
                    : null;

                LoadAnalysisResults(result.Response, _currentSnapshot);
                LoadInsights(_currentSnapshot, _comparison);

                HasAnalysis = true;
                ShowDashboard = true;
                OnPropertyChanged(nameof(ShowWelcome));
                OnPropertyChanged(nameof(ShowDetailsView));
                StatusText = $"Assessment complete • Risk: {_currentSnapshot.RiskScore.Level}";
            }
            else
            {
                StatusText = result.Error ?? "Assessment failed";
            }
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    private void ShowDetails()
    {
        ShowDashboard = false;
        OnPropertyChanged(nameof(ShowDetailsView));
    }

    [RelayCommand]
    private void ShowInsights()
    {
        ShowDashboard = true;
        OnPropertyChanged(nameof(ShowDetailsView));
    }

    [RelayCommand]
    private void ShowScoreExplainerToggle()
    {
        ShowScoreExplainer = !ShowScoreExplainer;
    }

    [RelayCommand]
    private void HideScoreExplainer()
    {
        ShowScoreExplainer = false;
    }

    [RelayCommand]
    private async Task ViewHistoryAsync()
    {
        if (_repoRoot is null) return;

        var snapshots = await _snapshotService.GetSnapshotsAsync(_repoRoot);
        StatusText = $"Found {snapshots.Count} historical snapshots";
    }

    partial void OnSelectedFileChanged(FileTreeItem? value)
    {
        if (value is not null && !value.IsDirectory)
        {
            LoadFileContent(value.FullPath);
            ShowDashboard = false;
        }
    }

    partial void OnSelectedDiagnosticChanged(DiagnosticItem? value)
    {
        if (value?.Diagnostic.File is not null && _repoRoot is not null)
        {
            var fullPath = Path.Combine(_repoRoot, value.Diagnostic.File);
            LoadFileContent(fullPath);
            ShowDashboard = false;
        }
    }

    partial void OnSelectedHotspotChanged(HotspotItem? value)
    {
        if (value?.Hotspot.File is not null && _repoRoot is not null)
        {
            var fullPath = Path.Combine(_repoRoot, value.Hotspot.File);
            LoadFileContent(fullPath);
            ShowDashboard = false;
        }
    }

    partial void OnSelectedActionChanged(ActionItemViewModel? value)
    {
        if (value?.Item.File is not null && _repoRoot is not null)
        {
            var fullPath = Path.Combine(_repoRoot, value.Item.File);
            LoadFileContent(fullPath);
            ShowDashboard = false;
        }
    }

    private async Task LoadRepoAsync(string path)
    {
        FileTree.Clear();
        CodeLines.Clear();
        Diagnostics.Clear();
        Hotspots.Clear();
        ActionItems.Clear();
        ScoreFactors.Clear();
        _currentResponse = null;
        _currentSnapshot = null;
        HasComparison = false;
        HasAnalysis = false;
        HasRepo = true;
        RepoName = Path.GetFileName(path);
        ShowDashboard = false;
        ShowScoreExplainer = false;
        OnPropertyChanged(nameof(ShowWelcome));
        OnPropertyChanged(nameof(ShowDetailsView));

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
            StatusText = $"Loaded: {path} • Last assessed: {latest.Timestamp:g}";
            RiskLevel = latest.RiskScore.Level;
            RiskScore = latest.RiskScore.Score;
            HasAnalysis = true;
            OnPropertyChanged(nameof(ShowWelcome));
            OnPropertyChanged(nameof(ShowDetailsView));
            ShowDashboard = true;

            // Load previous insights
            LoadInsights(latest, null);
        }
        else
        {
            StatusText = $"Loaded: {path} • Click 'Assess Risk' to analyze";
            RiskLevel = RiskLevel.Low;
            RiskScore = 0;
            InsightHeadline = "No assessment yet";
            InsightSubtext = "Click 'Assess Risk' to analyze this repository.";
            InsightWhyItMatters = "";
        }

        AnalysisStatus = null;
    }

    private void LoadInsights(Snapshot snapshot, SnapshotComparison? comparison)
    {
        // Generate headline insight
        var insight = _insightEngine.GenerateHeadlineInsight(snapshot);
        InsightHeadline = insight.Headline;
        InsightSubtext = insight.Subtext;
        InsightWhyItMatters = insight.WhyItMatters;
        InsightSeverity = insight.Severity;
        OnPropertyChanged(nameof(InsightColor));
        OnPropertyChanged(nameof(InsightBackgroundColor));

        // Generate metrics
        var metrics = _insightEngine.GenerateMetrics(snapshot, comparison);
        DuplicationPercent = metrics.DuplicationPercent;
        HotspotCount = metrics.HotspotCount;
        AffectedFiles = metrics.AffectedFiles;
        AffectedLines = metrics.AffectedLines;

        // Generate action items
        var actions = _insightEngine.GenerateActionItems(snapshot);
        ActionItems.Clear();
        foreach (var action in actions)
        {
            ActionItems.Add(new ActionItemViewModel { Item = action });
        }

        // Generate score explainer
        ScoreFactors.Clear();
        foreach (var factor in snapshot.RiskScore.Factors)
        {
            ScoreFactors.Add(new ScoreFactorViewModel { Factor = factor });
        }
        ScoreExplanation = $"Your risk score of {snapshot.RiskScore.Score} is calculated from {snapshot.RiskScore.Factors.Count} factors. " +
                          $"Each factor is weighted based on its impact on code maintainability.";

        // Generate trend insight if comparison available
        if (comparison is not null)
        {
            HasComparison = true;
            DiagnosticDelta = comparison.DiagnosticDelta;
            RiskTrend = comparison.Trend switch
            {
                TrendDirection.Improving => "improving",
                TrendDirection.Worsening => "worsening",
                _ => "stable"
            };

            var trendInsight = _insightEngine.GenerateTrendInsight(comparison);
            if (trendInsight is not null)
            {
                HasTrendInsight = true;
                TrendHeadline = trendInsight.Headline;
                TrendSubtext = trendInsight.Subtext;
            }
            else
            {
                HasTrendInsight = false;
            }

            OnPropertyChanged(nameof(TrendIcon));
            OnPropertyChanged(nameof(TrendColor));
        }
        else
        {
            HasComparison = false;
            HasTrendInsight = false;
        }
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
        OnPropertyChanged(nameof(HasCurrentFile));

        // Count issues in this file
        if (_repoRoot is not null && _currentResponse is not null)
        {
            var relativePath = Path.GetRelativePath(_repoRoot, path);
            CurrentFileIssueCount = _currentResponse.Diagnostics
                .Count(d => string.Equals(d.File, relativePath, StringComparison.OrdinalIgnoreCase));
        }

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
