using System.Text.Json.Serialization;

namespace CodeClone.Domain;

/// <summary>
/// Summary statistics from codeclone analysis.
/// </summary>
public sealed record AnalyzeSummary(
    [property: JsonPropertyName("total_diagnostics")]
    int TotalDiagnostics,

    [property: JsonPropertyName("total_files_analyzed")]
    int TotalFilesAnalyzed,

    [property: JsonPropertyName("by_code")]
    IReadOnlyDictionary<string, int>? ByCode = null,

    [property: JsonPropertyName("truncation")]
    TruncationInfo? Truncation = null
);

/// <summary>
/// Information about diagnostic truncation limits.
/// </summary>
public sealed record TruncationInfo(
    [property: JsonPropertyName("was_truncated")]
    bool WasTruncated,

    [property: JsonPropertyName("max_total")]
    int MaxTotal,

    [property: JsonPropertyName("max_per_file")]
    int MaxPerFile
);
