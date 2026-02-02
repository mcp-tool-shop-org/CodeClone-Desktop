using System.Text.Json;
using CodeClone.Domain;

namespace CodeClone.App.Services;

/// <summary>
/// Manages local snapshot storage for trend tracking.
/// All data stays on-device - privacy by design.
/// </summary>
public class SnapshotService
{
    private readonly string _storageDir;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SnapshotService()
    {
        _storageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodeClone-Desktop",
            "snapshots"
        );
        Directory.CreateDirectory(_storageDir);
    }

    /// <summary>
    /// Create and save a snapshot from analysis results.
    /// </summary>
    public async Task<Snapshot> CreateSnapshotAsync(
        string repoPath,
        AnalyzeResponse response,
        CancellationToken ct = default)
    {
        var gitInfo = await GetGitInfoAsync(repoPath, ct);
        var hotspots = CalculateHotspots(response);
        var riskScore = CalculateRiskScore(response, hotspots);

        var snapshot = new Snapshot(
            Id: Guid.NewGuid().ToString("N")[..12],
            RepoPath: repoPath,
            Timestamp: DateTime.UtcNow,
            GitCommit: gitInfo.commit,
            GitBranch: gitInfo.branch,
            RiskScore: riskScore,
            Summary: response.Summary,
            Hotspots: hotspots,
            Diagnostics: response.Diagnostics
        );

        await SaveSnapshotAsync(snapshot, ct);
        return snapshot;
    }

    /// <summary>
    /// Load all snapshots for a repository.
    /// </summary>
    public async Task<IReadOnlyList<Snapshot>> GetSnapshotsAsync(
        string repoPath,
        CancellationToken ct = default)
    {
        var repoHash = GetRepoHash(repoPath);
        var repoDir = Path.Combine(_storageDir, repoHash);

        if (!Directory.Exists(repoDir))
            return [];

        var snapshots = new List<Snapshot>();
        foreach (var file in Directory.GetFiles(repoDir, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var snapshot = JsonSerializer.Deserialize<Snapshot>(json, JsonOptions);
                if (snapshot is not null)
                    snapshots.Add(snapshot);
            }
            catch
            {
                // Skip corrupted snapshots
            }
        }

        return snapshots.OrderByDescending(s => s.Timestamp).ToList();
    }

    /// <summary>
    /// Get the most recent snapshot for comparison.
    /// </summary>
    public async Task<Snapshot?> GetLatestSnapshotAsync(
        string repoPath,
        CancellationToken ct = default)
    {
        var snapshots = await GetSnapshotsAsync(repoPath, ct);
        return snapshots.FirstOrDefault();
    }

    /// <summary>
    /// Compare two snapshots and return the delta.
    /// </summary>
    public SnapshotComparison Compare(Snapshot baseline, Snapshot current)
    {
        var diagnosticDelta = current.Summary.TotalDiagnostics - baseline.Summary.TotalDiagnostics;
        var scoreDelta = current.RiskScore.Score - baseline.RiskScore.Score;

        var newHotspots = current.Hotspots
            .Where(h => !baseline.Hotspots.Any(b => b.File == h.File))
            .ToList();

        var resolvedHotspots = baseline.Hotspots
            .Where(b => !current.Hotspots.Any(h => h.File == b.File))
            .ToList();

        var trend = scoreDelta switch
        {
            < -10 => TrendDirection.Improving,
            > 10 => TrendDirection.Worsening,
            _ => TrendDirection.Stable
        };

        return new SnapshotComparison(
            Baseline: baseline,
            Current: current,
            DiagnosticDelta: diagnosticDelta,
            ScoreDelta: scoreDelta,
            Trend: trend,
            NewHotspots: newHotspots,
            ResolvedHotspots: resolvedHotspots
        );
    }

    private async Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken ct)
    {
        var repoHash = GetRepoHash(snapshot.RepoPath);
        var repoDir = Path.Combine(_storageDir, repoHash);
        Directory.CreateDirectory(repoDir);

        var filename = $"{snapshot.Timestamp:yyyyMMdd-HHmmss}_{snapshot.Id}.json";
        var path = Path.Combine(repoDir, filename);

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    private static string GetRepoHash(string repoPath)
    {
        // Simple hash for folder organization
        var normalized = repoPath.Replace('\\', '/').ToLowerInvariant().TrimEnd('/');
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(normalized)
            )
        )[..16];
    }

    private static async Task<(string? commit, string? branch)> GetGitInfoAsync(
        string repoPath,
        CancellationToken ct)
    {
        try
        {
            var commitTask = RunGitCommandAsync(repoPath, "rev-parse --short HEAD", ct);
            var branchTask = RunGitCommandAsync(repoPath, "rev-parse --abbrev-ref HEAD", ct);

            await Task.WhenAll(commitTask, branchTask);

            return (commitTask.Result, branchTask.Result);
        }
        catch
        {
            return (null, null);
        }
    }

    private static async Task<string?> RunGitCommandAsync(
        string workDir,
        string args,
        CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process is null) return null;

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return process.ExitCode == 0 ? output.Trim() : null;
    }

    private static List<Hotspot> CalculateHotspots(AnalyzeResponse response)
    {
        return response.Diagnostics
            .Where(d => d.File is not null)
            .GroupBy(d => d.File!)
            .Select(g => new Hotspot(
                File: g.Key,
                DiagnosticCount: g.Count(),
                UncoveredLines: g.Count(d => d.Code == DiagnosticCodes.LineUncovered),
                Severity: g.Count() switch
                {
                    >= 10 => HotspotSeverity.Severe,
                    >= 5 => HotspotSeverity.Moderate,
                    _ => HotspotSeverity.Minor
                }
            ))
            .OrderByDescending(h => h.DiagnosticCount)
            .Take(10)
            .ToList();
    }

    private static RiskScore CalculateRiskScore(
        AnalyzeResponse response,
        IReadOnlyList<Hotspot> hotspots)
    {
        var factors = new List<RiskFactor>();

        // Factor 1: Total diagnostic count (weight: 30)
        var diagScore = Math.Min(response.Summary.TotalDiagnostics * 2, 100);
        factors.Add(new RiskFactor(
            Name: "Diagnostic Volume",
            Weight: 30,
            Value: diagScore,
            Description: $"{response.Summary.TotalDiagnostics} issues found"
        ));

        // Factor 2: Severe hotspot count (weight: 40)
        var severeCount = hotspots.Count(h => h.Severity == HotspotSeverity.Severe);
        var hotspotScore = Math.Min(severeCount * 25, 100);
        factors.Add(new RiskFactor(
            Name: "Hotspot Concentration",
            Weight: 40,
            Value: hotspotScore,
            Description: $"{severeCount} severe hotspots"
        ));

        // Factor 3: Analysis status (weight: 30)
        var statusScore = response.Status switch
        {
            AnalysisStatus.FAIL => 100,
            AnalysisStatus.PARTIAL => 50,
            _ => 0
        };
        factors.Add(new RiskFactor(
            Name: "Analysis Coverage",
            Weight: 30,
            Value: statusScore,
            Description: $"Status: {response.Status}"
        ));

        // Calculate weighted score
        var totalScore = factors.Sum(f => f.Weight * f.Value) / factors.Sum(f => f.Weight);

        var level = totalScore switch
        {
            >= 75 => RiskLevel.Critical,
            >= 50 => RiskLevel.High,
            >= 25 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        return new RiskScore(level, totalScore, factors);
    }
}

/// <summary>
/// Result of comparing two snapshots.
/// </summary>
public record SnapshotComparison(
    Snapshot Baseline,
    Snapshot Current,
    int DiagnosticDelta,
    int ScoreDelta,
    TrendDirection Trend,
    IReadOnlyList<Hotspot> NewHotspots,
    IReadOnlyList<Hotspot> ResolvedHotspots
);

public enum TrendDirection
{
    Improving,
    Stable,
    Worsening
}
