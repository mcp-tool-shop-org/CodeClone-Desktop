using System.Text.Json.Serialization;

namespace CodeClone.Domain;

/// <summary>
/// Top-level response from codeclone analyze command.
/// Maps to codeclone.analyze.response.schema.v0.1.json
/// </summary>
public sealed record AnalyzeResponse(
    [property: JsonPropertyName("schema_version")]
    string SchemaVersion,

    [property: JsonPropertyName("status")]
    AnalysisStatus Status,

    [property: JsonPropertyName("repo_root")]
    string RepoRoot,

    [property: JsonPropertyName("summary")]
    AnalyzeSummary Summary,

    [property: JsonPropertyName("diagnostics")]
    IReadOnlyList<Diagnostic> Diagnostics,

    [property: JsonPropertyName("timestamp")]
    DateTime? Timestamp = null
);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnalysisStatus
{
    [JsonPropertyName("OK")]
    OK,

    [JsonPropertyName("PARTIAL")]
    PARTIAL,

    [JsonPropertyName("FAIL")]
    FAIL
}
