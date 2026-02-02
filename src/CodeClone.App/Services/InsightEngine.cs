using CodeClone.Domain;

namespace CodeClone.App.Services;

/// <summary>
/// Transforms raw analysis data into human-readable insights.
/// The "So What?" layer that makes data actionable.
/// </summary>
public class InsightEngine
{
    /// <summary>
    /// Generate the main headline insight from analysis results.
    /// </summary>
    public Insight GenerateHeadlineInsight(Snapshot snapshot)
    {
        var level = snapshot.RiskScore.Level;
        var score = snapshot.RiskScore.Score;
        var hotspotCount = snapshot.Hotspots.Count(h => h.Severity == HotspotSeverity.Severe);
        var totalDiagnostics = snapshot.Summary.TotalDiagnostics;

        return level switch
        {
            RiskLevel.Critical => new Insight(
                Headline: "Code Quality Risk: CRITICAL",
                Subtext: $"{totalDiagnostics} issues detected across {snapshot.Summary.TotalFilesAnalyzed} files. {hotspotCount} severe hotspots require immediate attention.",
                WhyItMatters: "High duplication and uncovered code paths create compounding technical debt. Bug fixes here will likely require changes in multiple places, increasing regression risk.",
                Severity: InsightSeverity.Critical
            ),

            RiskLevel.High => new Insight(
                Headline: "Code Quality Risk: HIGH",
                Subtext: $"{totalDiagnostics} issues found. Most risk concentrated in {hotspotCount} files.",
                WhyItMatters: "Changes in these areas are likely to cause regressions and maintenance drag. Consider targeted refactoring before adding new features.",
                Severity: InsightSeverity.Warning
            ),

            RiskLevel.Medium => new Insight(
                Headline: "Code Quality: Needs Attention",
                Subtext: $"{totalDiagnostics} issues detected. Some areas could benefit from cleanup.",
                WhyItMatters: "Current duplication levels are manageable but trending toward problematic. Address hotspots proactively to prevent accumulation.",
                Severity: InsightSeverity.Warning
            ),

            _ => new Insight(
                Headline: "Code Quality: Good",
                Subtext: totalDiagnostics > 0
                    ? $"Only {totalDiagnostics} minor issues found. Codebase is well-maintained."
                    : "No significant issues detected. Keep up the good work!",
                WhyItMatters: "Low duplication means changes are isolated and maintainable. This codebase supports confident refactoring.",
                Severity: InsightSeverity.Info
            )
        };
    }

    /// <summary>
    /// Generate dashboard metrics in human-readable form.
    /// </summary>
    public DashboardMetrics GenerateMetrics(Snapshot snapshot, SnapshotComparison? comparison = null)
    {
        // Estimate duplication % based on diagnostics vs files
        var duplicationPercent = snapshot.Summary.TotalFilesAnalyzed > 0
            ? Math.Min((double)snapshot.Summary.TotalDiagnostics / (snapshot.Summary.TotalFilesAnalyzed * 10) * 100, 100)
            : 0;

        var affectedFiles = snapshot.Diagnostics
            .Where(d => d.File is not null)
            .Select(d => d.File)
            .Distinct()
            .Count();

        var affectedLines = snapshot.Diagnostics
            .Where(d => d.Line.HasValue)
            .Sum(d => (d.EndLine ?? d.Line!.Value) - d.Line!.Value + 1);

        TrendSummary? trend = null;
        if (comparison is not null)
        {
            var mainContributor = comparison.NewHotspots.FirstOrDefault()?.File
                ?? comparison.Current.Hotspots.FirstOrDefault()?.File
                ?? "N/A";

            trend = new TrendSummary(
                DuplicationDelta: comparison.ScoreDelta,
                NewHotspots: comparison.NewHotspots.Count,
                ResolvedHotspots: comparison.ResolvedHotspots.Count,
                MainContributor: Path.GetFileName(mainContributor)
            );
        }

        return new DashboardMetrics(
            DuplicationPercent: Math.Round(duplicationPercent, 1),
            HotspotCount: snapshot.Hotspots.Count(h => h.Severity != HotspotSeverity.Minor),
            AffectedFiles: affectedFiles,
            AffectedLines: affectedLines,
            Trend: trend
        );
    }

    /// <summary>
    /// Generate prioritized action items - "What should I fix first?"
    /// </summary>
    public IReadOnlyList<ActionItem> GenerateActionItems(Snapshot snapshot, int maxItems = 5)
    {
        var items = new List<ActionItem>();

        foreach (var hotspot in snapshot.Hotspots.Take(maxItems))
        {
            var priority = hotspot.Severity switch
            {
                HotspotSeverity.Severe => ActionPriority.Critical,
                HotspotSeverity.Moderate => ActionPriority.High,
                _ => ActionPriority.Medium
            };

            var impactScore = hotspot.DiagnosticCount * 10 + hotspot.UncoveredLines;
            var fileName = Path.GetFileName(hotspot.File);

            var (problem, recommendation, explanation) = GenerateProblemStatement(hotspot, snapshot);

            items.Add(new ActionItem(
                File: hotspot.File,
                Problem: problem,
                Recommendation: recommendation,
                Priority: priority,
                ImpactScore: impactScore,
                ImpactExplanation: explanation
            ));
        }

        return items.OrderByDescending(i => i.ImpactScore).ToList();
    }

    /// <summary>
    /// Generate insight-driven problem statement for a hotspot.
    /// </summary>
    private (string problem, string recommendation, string explanation) GenerateProblemStatement(
        Hotspot hotspot,
        Snapshot snapshot)
    {
        var fileName = Path.GetFileName(hotspot.File);
        var diagnosticsInFile = snapshot.Diagnostics
            .Where(d => d.File == hotspot.File)
            .ToList();

        var uncoveredCount = diagnosticsInFile.Count(d => d.Code == DiagnosticCodes.LineUncovered);
        var otherIssues = diagnosticsInFile.Count - uncoveredCount;

        // Determine primary problem type
        if (uncoveredCount > otherIssues && uncoveredCount > 5)
        {
            return (
                problem: $"{uncoveredCount} lines in {fileName} lack test coverage",
                recommendation: "Add unit tests for uncovered code paths, especially around business logic and error handling.",
                explanation: $"Any bug in these {uncoveredCount} lines will reach production undetected. Test coverage here would prevent regressions."
            );
        }

        if (hotspot.DiagnosticCount > 10)
        {
            return (
                problem: $"{fileName} has {hotspot.DiagnosticCount} issues concentrated in one file",
                recommendation: "Consider breaking this file into smaller, focused modules. High concentration suggests mixed responsibilities.",
                explanation: $"Changes here affect {hotspot.DiagnosticCount} potential problem areas. Splitting reduces blast radius of any single change."
            );
        }

        if (hotspot.Severity == HotspotSeverity.Severe)
        {
            return (
                problem: $"{fileName} is a critical quality hotspot",
                recommendation: "Prioritize cleanup here before adding new features. Extract common patterns into shared utilities.",
                explanation: "Severe hotspots multiply maintenance cost. Each bug fix may require changes in multiple places."
            );
        }

        return (
            problem: $"{fileName} has {hotspot.DiagnosticCount} issues worth reviewing",
            recommendation: "Review and address issues as part of normal development. No urgent action required.",
            explanation: "Moderate issues that can be addressed incrementally without blocking feature work."
        );
    }

    /// <summary>
    /// Generate trend insight for before/after comparison.
    /// </summary>
    public Insight? GenerateTrendInsight(SnapshotComparison comparison)
    {
        if (comparison.Trend == TrendDirection.Stable &&
            comparison.NewHotspots.Count == 0 &&
            comparison.ResolvedHotspots.Count == 0)
        {
            return null; // No significant change
        }

        if (comparison.Trend == TrendDirection.Worsening)
        {
            var mainCause = comparison.NewHotspots.FirstOrDefault()?.File ?? "recent changes";
            return new Insight(
                Headline: $"Quality declined since last analysis",
                Subtext: $"Risk score increased by {comparison.ScoreDelta} points. {comparison.NewHotspots.Count} new hotspot(s) detected.",
                WhyItMatters: $"Main contributor: {Path.GetFileName(mainCause)}. Consider reviewing recent changes in this area.",
                Severity: InsightSeverity.Warning
            );
        }

        if (comparison.Trend == TrendDirection.Improving)
        {
            return new Insight(
                Headline: "Quality improving! 🎉",
                Subtext: $"Risk score decreased by {Math.Abs(comparison.ScoreDelta)} points. {comparison.ResolvedHotspots.Count} hotspot(s) resolved.",
                WhyItMatters: "Your cleanup efforts are paying off. Keep this momentum going.",
                Severity: InsightSeverity.Info
            );
        }

        return null;
    }
}
