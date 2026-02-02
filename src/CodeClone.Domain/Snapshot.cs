using System.Text.Json.Serialization;

namespace CodeClone.Domain;

/// <summary>
/// A point-in-time snapshot of analysis results for trend tracking.
/// Stored locally - no cloud required.
/// </summary>
public sealed record Snapshot(
    [property: JsonPropertyName("id")]
    string Id,

    [property: JsonPropertyName("repo_path")]
    string RepoPath,

    [property: JsonPropertyName("timestamp")]
    DateTime Timestamp,

    [property: JsonPropertyName("git_commit")]
    string? GitCommit,

    [property: JsonPropertyName("git_branch")]
    string? GitBranch,

    [property: JsonPropertyName("risk_score")]
    RiskScore RiskScore,

    [property: JsonPropertyName("summary")]
    AnalyzeSummary Summary,

    [property: JsonPropertyName("hotspots")]
    IReadOnlyList<Hotspot> Hotspots,

    [property: JsonPropertyName("diagnostics")]
    IReadOnlyList<Diagnostic> Diagnostics
);

/// <summary>
/// Overall risk assessment - the "headline metric" for managers.
/// </summary>
public sealed record RiskScore(
    [property: JsonPropertyName("level")]
    RiskLevel Level,

    [property: JsonPropertyName("score")]
    int Score,  // 0-100

    [property: JsonPropertyName("factors")]
    IReadOnlyList<RiskFactor> Factors
);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RiskLevel
{
    [JsonPropertyName("low")]
    Low,

    [JsonPropertyName("medium")]
    Medium,

    [JsonPropertyName("high")]
    High,

    [JsonPropertyName("critical")]
    Critical
}

/// <summary>
/// Individual factor contributing to risk score.
/// </summary>
public sealed record RiskFactor(
    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonPropertyName("weight")]
    int Weight,

    [property: JsonPropertyName("value")]
    int Value,

    [property: JsonPropertyName("description")]
    string Description
);

/// <summary>
/// A duplication hotspot - file or region with high clone density.
/// </summary>
public sealed record Hotspot(
    [property: JsonPropertyName("file")]
    string File,

    [property: JsonPropertyName("diagnostic_count")]
    int DiagnosticCount,

    [property: JsonPropertyName("uncovered_lines")]
    int UncoveredLines,

    [property: JsonPropertyName("severity")]
    HotspotSeverity Severity
);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HotspotSeverity
{
    [JsonPropertyName("minor")]
    Minor,

    [JsonPropertyName("moderate")]
    Moderate,

    [JsonPropertyName("severe")]
    Severe
}
