using System.Text.Json.Serialization;

namespace CodeClone.Domain;

/// <summary>
/// Individual diagnostic finding from codeclone analysis.
/// </summary>
public sealed record Diagnostic(
    [property: JsonPropertyName("code")]
    string Code,

    [property: JsonPropertyName("severity")]
    DiagnosticSeverity Severity,

    [property: JsonPropertyName("message")]
    string Message,

    [property: JsonPropertyName("file")]
    string? File = null,

    [property: JsonPropertyName("line")]
    int? Line = null,

    [property: JsonPropertyName("column")]
    int? Column = null,

    [property: JsonPropertyName("end_line")]
    int? EndLine = null,

    [property: JsonPropertyName("end_column")]
    int? EndColumn = null,

    [property: JsonPropertyName("evidence")]
    IReadOnlyList<string>? Evidence = null,

    [property: JsonPropertyName("suggestions")]
    IReadOnlyList<string>? Suggestions = null
);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiagnosticSeverity
{
    [JsonPropertyName("error")]
    Error,

    [JsonPropertyName("warning")]
    Warning,

    [JsonPropertyName("info")]
    Info
}

/// <summary>
/// Known diagnostic codes emitted by codeclone CLI.
/// </summary>
public static class DiagnosticCodes
{
    public const string UntestedModule = "UNTESTED_MODULE";
    public const string LineUncovered = "LINE_UNCOVERED";
    public const string CoverageDataMissing = "COVERAGE_DATA_MISSING";
    public const string BranchUncovered = "BRANCH_UNCOVERED";
    public const string ParseError = "PARSE_ERROR";
}
