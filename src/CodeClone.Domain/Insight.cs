namespace CodeClone.Domain;

/// <summary>
/// Human-readable insight derived from analysis.
/// Answers "So what?" instead of just "What?".
/// </summary>
public sealed record Insight(
    string Headline,
    string Subtext,
    string WhyItMatters,
    InsightSeverity Severity
);

public enum InsightSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Actionable recommendation - "What should I fix first?".
/// </summary>
public sealed record ActionItem(
    string File,
    string Problem,
    string Recommendation,
    ActionPriority Priority,
    int ImpactScore,
    string ImpactExplanation
);

public enum ActionPriority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Dashboard metrics - human-readable, not technical.
/// </summary>
public sealed record DashboardMetrics(
    double DuplicationPercent,
    int HotspotCount,
    int AffectedFiles,
    int AffectedLines,
    TrendSummary? Trend
);

/// <summary>
/// Trend summary for before/after comparison.
/// </summary>
public sealed record TrendSummary(
    double DuplicationDelta,
    int NewHotspots,
    int ResolvedHotspots,
    string MainContributor
);
